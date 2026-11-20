namespace SmartLearner.App.Models;

/// <summary>Preference/preference.json 的内容。</summary>
public sealed class PreferenceFile
{
    public int Version { get; set; } = 1;

    public List<string> Keywords { get; set; } = new();
}
