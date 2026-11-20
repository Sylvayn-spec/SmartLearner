using System.Security.Cryptography;
using System.Text;

namespace SmartLearner.App.Infrastructure;

/// <summary>
/// Access Secret 的本地加密（X-03）。使用 Windows DPAPI，绑定当前用户。
/// 任何情况下不落明文。
/// </summary>
public static class Dpapi
{
    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string? Unprotect(string? protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(protectedText);
            var plain = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Secret 解密失败（可能换了用户或机器）：{ex.Message}");
            return null;
        }
    }
}
