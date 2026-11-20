using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartLearner.App.Models;

/// <summary>一条持久化的对话消息。</summary>
public sealed class ChatMessageData
{
    /// <summary>user / assistant。</summary>
    public string Role { get; set; } = "user";

    public string Text { get; set; } = string.Empty;

    public List<string> Sources { get; set; } = new();

    public List<ChatArticle> Articles { get; set; } = new();

    public bool CanDelete { get; set; } = true;
}

/// <summary>chat/&lt;id&gt;.json 的内容：一次会话。</summary>
public sealed partial class ChatSession : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _title = "新对话";

    /// <summary>所属课程；切换课程时按此过滤。</summary>
    public string Course { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public List<ChatMessageData> Messages { get; set; } = new();

    [ObservableProperty]
    private bool _isSelected;
}
