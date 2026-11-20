using SmartLearner.App.Infrastructure;
using SmartLearner.App.Models;

namespace SmartLearner.App.Services;

/// <summary>
/// 推送偏好的独立存储：SmartLearner/Preference/preference.json。
/// 最多 6 个关键词。
/// </summary>
public sealed class PreferenceService
{
    public const int MaxCount = 6;

    private PreferenceFile _file = new();

    public event EventHandler? Changed;

    public IReadOnlyList<string> Keywords => _file.Keywords;

    public bool IsEmpty => _file.Keywords.Count == 0;

    public void Load()
    {
        AppPaths.EnsureCreated();
        _file = JsonStore.Load(AppPaths.PreferenceFile, () => new PreferenceFile());
        Normalize();
    }

    /// <summary>添加一个关键词；已存在、为空或超上限时返回 false。</summary>
    public bool Add(string? keyword)
    {
        var value = (keyword ?? string.Empty).Trim();
        if (value.Length == 0 ||
            _file.Keywords.Count >= MaxCount ||
            _file.Keywords.Any(k => string.Equals(k, value, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        _file.Keywords.Add(value);
        Save();
        return true;
    }

    public void Remove(string keyword)
    {
        if (_file.Keywords.RemoveAll(k => string.Equals(k, keyword, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            Save();
        }
    }

    /// <summary>从旧版 settings.json 迁移一次。</summary>
    public void ImportLegacy(IEnumerable<string> keywords)
    {
        if (!IsEmpty)
        {
            return;
        }

        foreach (var keyword in keywords.Take(MaxCount))
        {
            Add(keyword);
        }
    }

    private void Normalize()
    {
        _file.Keywords = _file.Keywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxCount)
            .ToList();
    }

    private void Save()
    {
        Normalize();
        JsonStore.Save(AppPaths.PreferenceFile, _file);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
