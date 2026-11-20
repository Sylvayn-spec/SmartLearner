using System.IO;
using System.Text.Json;

namespace SmartLearner.App.Infrastructure;

/// <summary>
/// JSON 读写。所有写入走“临时文件 + 替换”，避免半截文件（X-04）。
/// </summary>
public static class JsonStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>读取；文件不存在或损坏时返回 fallback，并把损坏文件备份为 .corrupt。</summary>
    public static T Load<T>(string path, Func<T> fallback)
    {
        try
        {
            if (!File.Exists(path))
            {
                return fallback();
            }

            var json = File.ReadAllText(path);
            var value = JsonSerializer.Deserialize<T>(json, Options);
            return value ?? fallback();
        }
        catch (Exception ex)
        {
            Logger.Warn($"读取 {Path.GetFileName(path)} 失败，已回退默认值：{ex.Message}");
            TryBackupCorrupt(path);
            return fallback();
        }
    }

    /// <summary>写入；先写 .tmp 再替换，异常时不影响原文件。</summary>
    public static void Save<T>(string path, T value)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var temp = path + ".tmp";
        var json = JsonSerializer.Serialize(value, Options);
        File.WriteAllText(temp, json);
        File.Move(temp, path, overwrite: true);
    }

    private static void TryBackupCorrupt(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var backup = $"{path}.{DateTime.Now:yyyyMMddHHmmss}.corrupt";
            File.Move(path, backup, overwrite: true);
        }
        catch (Exception ex)
        {
            Logger.Error("备份损坏文件失败", ex);
        }
    }
}
