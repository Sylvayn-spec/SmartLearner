using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartLearner.App.Models;

/// <summary>知识库中的一个文本片段。</summary>
public sealed class KnowledgeChunk
{
    public string File { get; set; } = string.Empty;

    public int Page { get; set; }

    public string Text { get; set; } = string.Empty;
}

/// <summary>已索引文件的元信息。</summary>
public sealed class KnowledgeFileInfo
{
    public string File { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;

    public int Chunks { get; set; }

    public DateTime IndexedAt { get; set; }
}

/// <summary>Database/&lt;课程&gt;/chunks.json 的内容。</summary>
public sealed class KnowledgeBase
{
    public int Dimension { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<KnowledgeChunk> Chunks { get; set; } = new();

    public List<KnowledgeFileInfo> Files { get; set; } = new();

    /// <summary>与 Chunks 一一对应的向量，存在 vectors.bin，不进入 JSON。</summary>
    [JsonIgnore]
    public List<float[]> Vectors { get; set; } = new();
}

public sealed record IndexProgress(int Done, int Total, string Message);

public sealed record SearchHit(KnowledgeChunk Chunk, float Score);

/// <summary>课程助手左侧的文件勾选项。</summary>
public partial class AssistantFileItem : ObservableObject
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public string SizeText { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _statusText = "未建库";
}

public enum ChatRole
{
    User,
    Assistant
}

/// <summary>对话里展示的知乎文章卡片。</summary>
public sealed class ChatArticle
{
    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Excerpt { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;
}

/// <summary>对话气泡。</summary>
public sealed partial class ChatMessage : ObservableObject
{
    public ChatRole Role { get; init; }

    public string Text { get; init; } = string.Empty;

    public IReadOnlyList<string> Sources { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ChatArticle> Articles { get; init; } = Array.Empty<ChatArticle>();

    /// <summary>欢迎语等系统消息不允许删除。</summary>
    public bool CanDelete { get; init; } = true;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _showSelection;

    public bool HasSources => Sources.Count > 0;

    public bool HasArticles => Articles.Count > 0;

    public string SourceText => string.Join("　", Sources.Select(s => "来源 · " + s));

    /// <summary>复制到剪贴板时使用的纯文本。</summary>
    public string PlainText
    {
        get
        {
            var parts = new List<string> { Text };

            if (HasSources)
            {
                parts.Add(SourceText);
            }

            if (HasArticles)
            {
                parts.AddRange(Articles.Select(article => $"{article.Title}\n{article.Url}"));
            }

            return string.Join("\n\n", parts);
        }
    }
}
