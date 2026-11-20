using SmartLearner.App.Infrastructure;
using SmartLearner.App.Models;

namespace SmartLearner.App.Services;

/// <summary>
/// 阿里百炼 API Key 的独立存储：SmartLearner/AccessKey/bailian.json。
/// 文件里存的是 DPAPI 加密后的值，不是明文。
/// </summary>
public sealed class BailianCredentialService
{
    private BailianCredential _credential = new();

    public event EventHandler? Changed;

    public void Load()
    {
        AppPaths.EnsureCreated();
        _credential = JsonStore.Load(AppPaths.BailianKeyFile, () => new BailianCredential());
    }

    public string? Key => Dpapi.Unprotect(_credential.AccessKey);

    public bool HasKey => !string.IsNullOrWhiteSpace(Key);

    public bool IsExpired => _credential.Expired;

    public DateTime SavedAt => _credential.SavedAt;

    public void SetKey(string? plainText)
    {
        _credential = new BailianCredential
        {
            AccessKey = string.IsNullOrWhiteSpace(plainText) ? null : Dpapi.Protect(plainText.Trim()),
            SavedAt = DateTime.Now,
            Expired = false
        };

        Save();
    }

    public void MarkExpired(bool expired)
    {
        if (_credential.Expired == expired)
        {
            return;
        }

        _credential.Expired = expired;
        Save();
    }

    /// <summary>从旧版 settings.json 迁移一次。</summary>
    public void ImportLegacy(string? plainText)
    {
        if (!HasKey && !string.IsNullOrWhiteSpace(plainText))
        {
            SetKey(plainText);
        }
    }

    private void Save()
    {
        AppPaths.EnsureCreated();
        JsonStore.Save(AppPaths.BailianKeyFile, _credential);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
