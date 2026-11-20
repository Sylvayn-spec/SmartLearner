using System.Text;

namespace SmartLearner.App.Services;

public sealed record OrganizeProgress(int Done, int Total, string Message);

/// <summary>
/// 课件整理：抽取文本 → 分段 → 逐段交给模型提炼 → 合并成 Markdown。
/// </summary>
public sealed class OrganizeService
{
    private const int ChunkSize = 5500;

    private const string SystemPrompt =
        "你是学习助手。请把用户提供的课件内容整理成中文知识点笔记。\n" +
        "要求：\n" +
        "1. 用 Markdown 输出：`#` 为章标题，`##` 为小节，`-` 为要点；\n" +
        "2. 保留术语的英文原文，例如“过拟合（overfitting）”；\n" +
        "3. 公式用 LaTeX 保留，不要强行改写；\n" +
        "4. 只整理原文出现的内容，不要编造；\n" +
        "5. 不要输出解释性前后缀，直接给 Markdown。";

    private readonly BailianClient _ai;

    public OrganizeService(BailianClient ai) => _ai = ai;

    public async Task<string> GenerateAsync(
        string filePath,
        IProgress<OrganizeProgress>? progress,
        CancellationToken token)
    {
        if (!_ai.IsConfigured)
        {
            throw new InvalidOperationException("还没有配置百炼 API Key，请先到「设置」填写。");
        }

        progress?.Report(new OrganizeProgress(0, 1, "正在抽取文本…"));
        var text = TextExtractor.Extract(filePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("没有抽取到文本，可能是扫描件或空文件。");
        }

        var chunks = Split(text, ChunkSize);
        var builder = new StringBuilder();

        for (var i = 0; i < chunks.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            progress?.Report(new OrganizeProgress(i, chunks.Count, $"正在整理第 {i + 1}/{chunks.Count} 段…"));

            var prompt = $"以下是课件内容的第 {i + 1}/{chunks.Count} 段，请按要求整理：\n\n{chunks[i]}";
            var markdown = await _ai.ChatAsync(SystemPrompt, prompt, token);

            builder.AppendLine(markdown.Trim());
            builder.AppendLine();
        }

        progress?.Report(new OrganizeProgress(chunks.Count, chunks.Count, "整理完成"));
        return builder.ToString().Trim();
    }

    /// <summary>按段落聚合切块，尽量不把一句话拆断。</summary>
    private static List<string> Split(string text, int size)
    {
        var paragraphs = text
            .Replace("\r\n", "\n")
            .Split(new[] { "\n\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            var piece = paragraph.Trim();
            if (piece.Length == 0)
            {
                continue;
            }

            if (current.Length + piece.Length + 2 > size && current.Length > 0)
            {
                chunks.Add(current.ToString());
                current.Clear();
            }

            current.AppendLine(piece);
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString());
        }

        if (chunks.Count == 0)
        {
            chunks.Add(text);
        }

        return chunks;
    }
}
