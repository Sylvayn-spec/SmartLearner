using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SmartLearner.App.Infrastructure;
using SmartLearner.App.Models;

namespace SmartLearner.App.Services;

/// <summary>
/// 课程知识库：文本分块 → 向量化 → 本地存储 → 余弦检索。
/// 存储位置：Database/&lt;课程&gt;/chunks.json + vectors.bin
/// </summary>
public sealed class KnowledgeBaseService
{
    private const int ChunkSize = 420;
    private const int Overlap = 60;
    private const int BatchSize = 10;

    private static readonly Regex PagePattern = new(@"\[第\s*(\d+)\s*页\]", RegexOptions.Compiled);

    private const string OcrPrompt =
        "请提取图片中的全部文字，保留标题层级、列表和公式；不要添加任何解释或总结。";

    private readonly BailianClient _ai;
    private readonly SettingsService _settings;

    public KnowledgeBaseService(BailianClient ai, SettingsService settings)
    {
        _ai = ai;
        _settings = settings;
    }

    public string Directory(Course course) =>
        Path.Combine(AppPaths.Database, AppPaths.SanitizeFileName(course.Name));

    public bool HasIndex(Course course) => File.Exists(ChunksPath(course));

    public KnowledgeBase Load(Course course)
    {
        var knowledge = JsonStore.Load(ChunksPath(course), () => new KnowledgeBase());
        knowledge.Vectors = ReadVectors(VectorsPath(course), knowledge.Dimension, knowledge.Chunks.Count);
        return knowledge;
    }

    public void Clear(Course course)
    {
        var directory = Directory(course);
        if (System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>移除指定文件的索引，其余文件保持不变。</summary>
    public void RemoveFiles(Course course, IEnumerable<string> fileNames)
    {
        var knowledge = Load(course);
        foreach (var name in fileNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            RemoveFile(knowledge, name);
        }

        knowledge.UpdatedAt = DateTime.Now;
        Save(course, knowledge);
    }

    public async Task<KnowledgeBase> IndexAsync(
        Course course,
        IReadOnlyList<string> files,
        IProgress<IndexProgress>? progress,
        CancellationToken token)
    {
        var knowledge = Load(course);
        System.IO.Directory.CreateDirectory(Directory(course));

        for (var i = 0; i < files.Count; i++)
        {
            token.ThrowIfCancellationRequested();

            var path = files[i];
            var name = Path.GetFileName(path);
            progress?.Report(new IndexProgress(i, files.Count, $"正在解析 {name}…"));

            string hash;
            try
            {
                hash = ComputeHash(path);
            }
            catch (Exception ex)
            {
                Logger.Warn($"读取 {name} 失败：{ex.Message}");
                continue;
            }

            if (knowledge.Files.Any(f => f.File == name && f.Hash == hash))
            {
                progress?.Report(new IndexProgress(i, files.Count, $"{name} 已是最新，跳过"));
                continue;
            }

            string text;
            try
            {
                text = TextExtractor.Extract(path);
            }
            catch (Exception ex)
            {
                Logger.Warn($"解析 {name} 失败：{ex.Message}");
                continue;
            }

            // 文本层太薄 → 视为扫描件，走 OCR
            var pageHints = Math.Max(1, PagePattern.Matches(text).Count);
            var isPdf = Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
            if (isPdf && text.Length / pageHints < 40)
            {
                progress?.Report(new IndexProgress(i, files.Count, $"{name} 没有文本层，正在 OCR…"));
                try
                {
                    text = await OcrPdfAsync(path, i, files.Count, progress, token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"OCR 失败：{name}，{ex.Message}");
                }
            }

            var chunks = BuildChunks(name, text);
            Logger.Info($"索引 {name}：抽取 {text.Length} 字，分块 {chunks.Count} 个");

            if (chunks.Count == 0)
            {
                RemoveFile(knowledge, name);
                knowledge.Files.Add(new KnowledgeFileInfo
                {
                    File = name,
                    Hash = hash,
                    Chunks = 0,
                    IndexedAt = DateTime.Now
                });

                progress?.Report(new IndexProgress(i, files.Count, $"{name} 没有文本层（可能是扫描件），已跳过"));
                continue;
            }

            var vectors = new List<float[]>();
            for (var offset = 0; offset < chunks.Count; offset += BatchSize)
            {
                token.ThrowIfCancellationRequested();
                var batch = chunks.Skip(offset).Take(BatchSize).Select(c => c.Text).ToList();
                var embedded = await _ai.EmbeddingsAsync(batch, token);
                vectors.AddRange(embedded);

                progress?.Report(new IndexProgress(
                    i,
                    files.Count,
                    $"正在向量化 {name}（{Math.Min(offset + BatchSize, chunks.Count)}/{chunks.Count}）"));

                await Task.Delay(120, token);
            }

            RemoveFile(knowledge, name);
            knowledge.Chunks.AddRange(chunks);
            knowledge.Vectors.AddRange(vectors);
            knowledge.Files.Add(new KnowledgeFileInfo
            {
                File = name,
                Hash = hash,
                Chunks = chunks.Count,
                IndexedAt = DateTime.Now
            });

            if (knowledge.Dimension == 0 && vectors.Count > 0)
            {
                knowledge.Dimension = vectors[0].Length;
            }
        }

        knowledge.UpdatedAt = DateTime.Now;
        Save(course, knowledge);

        var skippedFiles = knowledge.Files
            .Where(f => f.Chunks == 0)
            .Select(f => f.File)
            .Distinct()
            .ToList();

        progress?.Report(new IndexProgress(
            files.Count,
            files.Count,
            skippedFiles.Count == 0
                ? "建库完成"
                : $"建库完成；{string.Join("、", skippedFiles)} 无文本层"));

        return knowledge;
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        Course course,
        string query,
        int topK,
        CancellationToken token)
    {
        var knowledge = Load(course);
        if (knowledge.Chunks.Count == 0 || knowledge.Vectors.Count == 0)
        {
            return Array.Empty<SearchHit>();
        }

        var queryVector = await _ai.EmbeddingAsync(query, token);
        if (queryVector.Length == 0)
        {
            return Array.Empty<SearchHit>();
        }

        var hits = new List<SearchHit>(knowledge.Chunks.Count);
        for (var i = 0; i < knowledge.Chunks.Count && i < knowledge.Vectors.Count; i++)
        {
            hits.Add(new SearchHit(knowledge.Chunks[i], Cosine(queryVector, knowledge.Vectors[i])));
        }

        return hits
            .OrderByDescending(hit => hit.Score)
            .Take(topK)
            .ToList();
    }

    private async Task<string> OcrPdfAsync(
        string path,
        int fileIndex,
        int fileCount,
        IProgress<IndexProgress>? progress,
        CancellationToken token)
    {
        var pages = await Task.Run(() => PdfPageRenderer.Render(path), token);
        if (pages.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < pages.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            progress?.Report(new IndexProgress(fileIndex, fileCount, $"OCR 识别第 {i + 1}/{pages.Count} 页…"));

            var content = await _ai.VisionAsync(
                _settings.Current.OcrModel,
                OcrPrompt,
                pages[i].Png,
                token);

            builder.AppendLine($"[第 {pages[i].Page} 页]");
            builder.AppendLine(content.Trim());
            await Task.Delay(150, token);
        }

        return builder.ToString();
    }

    private static List<KnowledgeChunk> BuildChunks(string fileName, string text)
    {
        var chunks = new List<KnowledgeChunk>();
        foreach (var (page, pageText) in SplitPages(text))
        {
            foreach (var piece in ChunkText(pageText))
            {
                chunks.Add(new KnowledgeChunk { File = fileName, Page = page, Text = piece });
            }
        }

        return chunks;
    }

    private static IEnumerable<(int Page, string Text)> SplitPages(string text)
    {
        var matches = PagePattern.Matches(text);
        if (matches.Count == 0)
        {
            yield return (0, text);
            yield break;
        }

        for (var i = 0; i < matches.Count; i++)
        {
            var start = matches[i].Index + matches[i].Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            if (int.TryParse(matches[i].Groups[1].Value, out var page) && end > start)
            {
                yield return (page, text[start..end]);
            }
        }
    }

    private static IEnumerable<string> ChunkText(string text)
    {
        var clean = Regex.Replace(text, @"\s+", " ").Trim();
        if (clean.Length == 0)
        {
            yield break;
        }

        var index = 0;
        while (index < clean.Length)
        {
            var length = Math.Min(ChunkSize, clean.Length - index);
            var piece = clean.Substring(index, length).Trim();
            if (piece.Length > 8)
            {
                yield return piece;
            }

            if (index + length >= clean.Length)
            {
                yield break;
            }

            index += ChunkSize - Overlap;
        }
    }

    private void RemoveFile(KnowledgeBase knowledge, string fileName)
    {
        for (var i = knowledge.Chunks.Count - 1; i >= 0; i--)
        {
            if (knowledge.Chunks[i].File != fileName)
            {
                continue;
            }

            knowledge.Chunks.RemoveAt(i);
            if (i < knowledge.Vectors.Count)
            {
                knowledge.Vectors.RemoveAt(i);
            }
        }

        knowledge.Files.RemoveAll(f => f.File == fileName);
    }

    private void Save(Course course, KnowledgeBase knowledge)
    {
        System.IO.Directory.CreateDirectory(Directory(course));
        WriteVectors(VectorsPath(course), knowledge.Vectors);
        JsonStore.Save(ChunksPath(course), knowledge);
    }

    private string ChunksPath(Course course) => Path.Combine(Directory(course), "chunks.json");

    private string VectorsPath(Course course) => Path.Combine(Directory(course), "vectors.bin");

    private static List<float[]> ReadVectors(string path, int dimension, int expectedCount)
    {
        var result = new List<float[]>();
        if (!File.Exists(path) || dimension <= 0)
        {
            return result;
        }

        var bytes = File.ReadAllBytes(path);
        var size = dimension * sizeof(float);
        for (var offset = 0; offset + size <= bytes.Length && result.Count < expectedCount; offset += size)
        {
            var vector = new float[dimension];
            Buffer.BlockCopy(bytes, offset, vector, 0, size);
            result.Add(vector);
        }

        return result;
    }

    private static void WriteVectors(string path, IReadOnlyList<float[]> vectors)
    {
        using var stream = File.Create(path);
        foreach (var vector in vectors)
        {
            var bytes = new byte[vector.Length * sizeof(float)];
            Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }
    }

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static float Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
        {
            return 0;
        }

        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        return na == 0 || nb == 0 ? 0 : (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
    }
}
