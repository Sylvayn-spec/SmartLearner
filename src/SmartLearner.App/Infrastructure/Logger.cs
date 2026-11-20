using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartLearner.App.Infrastructure;

/// <summary>
/// 按天滚动的本地日志（X-02）。写入前统一脱敏 Secret 与 Bearer Token。
/// </summary>
public static class Logger
{
    private static readonly object Gate = new();
    private static readonly Regex SecretPattern = new(
        @"(?i)(bearer\s+[a-z0-9\-\._~\+\/]+=*|access[_\-]?secret\s*[:=]\s*\S+|authorization\s*[:=]\s*\S+)",
        RegexOptions.Compiled);

    private static string _logDir = AppPaths.Logs;

    public static void Initialize(string logDirectory)
    {
        _logDir = logDirectory;
        Directory.CreateDirectory(_logDir);
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message) => Write("WARN", message, null);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(_logDir);
                var path = Path.Combine(_logDir, $"{DateTime.Now:yyyy-MM-dd}.log");
                var line = new StringBuilder()
                    .Append(DateTime.Now.ToString("HH:mm:ss.fff"))
                    .Append(" [").Append(level).Append("] ")
                    .Append(Sanitize(message));

                if (ex is not null)
                {
                    line.Append(" | ").Append(ex.GetType().Name).Append(": ").Append(Sanitize(ex.Message));
                }

                File.AppendAllText(path, line.AppendLine().ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // 日志失败不能影响主流程。
        }
    }

    private static string Sanitize(string text) =>
        SecretPattern.Replace(text, m => m.Value.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase)
            ? "Bearer ***"
            : "***");
}
