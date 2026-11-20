namespace SmartLearner.App.Models;

/// <summary>AccessKey/bailian.json 的内容。AccessKey 用 DPAPI 加密后存放。</summary>
public sealed class BailianCredential
{
    public string? AccessKey { get; set; }

    public DateTime SavedAt { get; set; }

    /// <summary>最近一次调用是否被判为无效（401/403）。</summary>
    public bool Expired { get; set; }
}
