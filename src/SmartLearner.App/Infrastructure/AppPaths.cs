using System.IO;

namespace SmartLearner.App.Infrastructure;

/// <summary>
/// 应用数据目录。默认 桌面/SmartLearner，可在设置中修改。
/// 首次启动负责创建全部子目录（S-05）。
/// </summary>
public static class AppPaths
{
    private static readonly object Gate = new();

    public static string Root { get; private set; } = DefaultRoot;

    public static string DefaultRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "SmartLearner");

    public static string Documents => Path.Combine(Root, "Documents");
    public static string Database => Path.Combine(Root, "Database");
    public static string Cache => Path.Combine(Root, "cache");
    public static string Logs => Path.Combine(Root, "logs");
    public static string Trash => Path.Combine(Root, ".trash");

    /// <summary>知乎凭证目录与文件。</summary>
    public static string AccessKeyDir => Path.Combine(Root, "AccessKey");
    public static string ZhihuKeyFile => Path.Combine(AccessKeyDir, "zhihu.json");
    public static string BailianKeyFile => Path.Combine(AccessKeyDir, "bailian.json");

    /// <summary>推送偏好目录与文件。</summary>
    public static string PreferenceDir => Path.Combine(Root, "Preference");
    public static string PreferenceFile => Path.Combine(PreferenceDir, "preference.json");

    /// <summary>知乎推送的周缓存。</summary>
    public static string ZhihuFeedCache => Path.Combine(Cache, "zhihu-week.json");

    /// <summary>配置文件固定放在默认目录，避免改了数据目录后找不到配置。</summary>
    public static string SettingsFile => Path.Combine(DefaultRoot, "settings.json");
    public static string CoursesFile => Path.Combine(Root, "courses.json");
    public static string KeywordsFile => Path.Combine(Root, "keywords.json");

    /// <summary>切换数据根目录并确保目录存在。传入空值回落到默认目录。</summary>
    public static void Initialize(string? root)
    {
        lock (Gate)
        {
            Root = string.IsNullOrWhiteSpace(root)
                ? DefaultRoot
                : Path.GetFullPath(root.Trim());
            EnsureCreated();
        }
    }

    /// <summary>创建所有必需目录，可重复调用。</summary>
    public static void EnsureCreated()
    {
        foreach (var dir in new[] { Root, Documents, Database, Cache, Logs, Trash, AccessKeyDir, PreferenceDir })
        {
            Directory.CreateDirectory(dir);
        }
    }

    /// <summary>把用户输入的文件名清洗成合法文件名。</summary>
    public static string SanitizeFileName(string? name, string fallback = "未命名")
    {
        var value = (name ?? string.Empty).Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        value = value.Trim('.', ' ');
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
