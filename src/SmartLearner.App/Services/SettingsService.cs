namespace SmartLearner.App.Services;

using SmartLearner.App.Infrastructure;
using SmartLearner.App.Models;

/// <summary>
/// 设置的加载、保存与变更通知。
/// settings.json 固定在默认目录（桌面/SmartLearner），数据目录可另行配置，
/// 这样即使数据目录被改坏，也能找回来改回去。
/// </summary>
public sealed class SettingsService
{
    private readonly object _gate = new();

    public AppSettings Current { get; private set; } = new();

    public event EventHandler? Changed;

    public void Load()
    {
        lock (_gate)
        {
            Current = JsonStore.Load(AppPaths.SettingsFile, () => new AppSettings());
            Normalize();
        }
    }

    public void Save()
    {
        lock (_gate)
        {
            Normalize();
            JsonStore.Save(AppPaths.SettingsFile, Current);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public string? AccessSecret => Dpapi.Unprotect(Current.ProtectedAccessSecret);

    public bool HasAccessSecret => !string.IsNullOrWhiteSpace(AccessSecret);

    public void SetAccessSecret(string? plainText)
    {
        Current.ProtectedAccessSecret = string.IsNullOrWhiteSpace(plainText)
            ? null
            : Dpapi.Protect(plainText.Trim());
        Save();
    }

    public void SetDarkMode(bool dark)
    {
        if (Current.IsDarkMode == dark)
        {
            return;
        }

        Current.IsDarkMode = dark;
        Save();
    }

    public void SetDataRoot(string? root)
    {
        Current.DataRoot = string.IsNullOrWhiteSpace(root) ? null : root.Trim();
        Save();
        AppPaths.Initialize(Current.DataRoot);
        Logger.Initialize(AppPaths.Logs);
    }

    private void Normalize()
    {
        if (Current.CacheTtlMinutes <= 0)
        {
            Current.CacheTtlMinutes = 30;
        }

        // 早期版本把对话模型写成了 deepseek-flash，百炼上不存在，统一迁到 qwen-flash。
        if (string.IsNullOrWhiteSpace(Current.ChatModel) ||
            string.Equals(Current.ChatModel, "deepseek-flash", StringComparison.OrdinalIgnoreCase))
        {
            Current.ChatModel = "qwen-flash";
        }

        if (string.IsNullOrWhiteSpace(Current.EmbeddingModel))
        {
            Current.EmbeddingModel = "text-embedding-v4";
        }

        if (string.IsNullOrWhiteSpace(Current.OcrModel))
        {
            Current.OcrModel = "qwen-vl-ocr";
        }

        Current.Keywords = Current.Keywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
    }
}
