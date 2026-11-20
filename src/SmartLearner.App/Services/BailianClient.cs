using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SmartLearner.App.Infrastructure;

namespace SmartLearner.App.Services;

/// <summary>
/// 阿里百炼（DashScope 兼容 OpenAI 模式）客户端。
/// 统一走 /chat/completions 与 /embeddings。
/// </summary>
public sealed class BailianClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    private readonly SettingsService _settings;
    private readonly BailianCredentialService _credentials;

    public BailianClient(SettingsService settings, BailianCredentialService credentials)
    {
        _settings = settings;
        _credentials = credentials;
    }

    public bool IsConfigured => _credentials.HasKey;

    public async Task<string> ChatAsync(string systemPrompt, string userPrompt, CancellationToken token)
    {
        var key = _credentials.Key;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("尚未配置百炼 API Key，请先到「设置」填写。");
        }

        var payload = new
        {
            model = _settings.Current.ChatModel,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.3
        };

        var url = _settings.Current.ApiBase.TrimEnd('/') + "/chat/completions";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await Http.SendAsync(request, token);
        var body = await response.Content.ReadAsStringAsync(token);

        if (!response.IsSuccessStatusCode)
        {
            Logger.Warn($"百炼调用失败：{(int)response.StatusCode} {body}");

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden ||
                body.Contains("InvalidApiKey", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase))
            {
                _credentials.MarkExpired(true);
                throw new InvalidOperationException("百炼 API Key 已过期或无效，请到「设置」更新。");
            }

            throw new InvalidOperationException($"百炼返回 {(int)response.StatusCode}：{Trim(body)}");
        }

        using var document = JsonDocument.Parse(body);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? string.Empty;
    }

    public async Task<float[]> EmbeddingAsync(string text, CancellationToken token) =>
        (await EmbeddingsAsync(new[] { text }, token)).FirstOrDefault() ?? Array.Empty<float>();

    /// <summary>批量向量化（百炼单次最多 10 条）。</summary>
    public async Task<List<float[]>> EmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken token)
    {
        var key = _credentials.Key;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("尚未配置百炼 API Key，请先到「设置」填写。");
        }

        var payload = new
        {
            model = _settings.Current.EmbeddingModel,
            input = texts
        };

        var url = _settings.Current.ApiBase.TrimEnd('/') + "/embeddings";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await Http.SendAsync(request, token);
        var body = await response.Content.ReadAsStringAsync(token);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                _credentials.MarkExpired(true);
                throw new InvalidOperationException("百炼 API Key 已过期或无效，请到「设置」更新。");
            }

            throw new InvalidOperationException($"百炼向量化失败 {(int)response.StatusCode}：{Trim(body)}");
        }

        using var document = JsonDocument.Parse(body);
        var result = new List<float[]>();
        foreach (var item in document.RootElement.GetProperty("data").EnumerateArray())
        {
            var vector = item.GetProperty("embedding")
                .EnumerateArray()
                .Select(value => value.GetSingle())
                .ToArray();
            result.Add(vector);
        }

        return result;
    }

    /// <summary>视觉模型：把一张图片（PNG）交给千问多模态模型识别。</summary>
    public async Task<string> VisionAsync(
        string model,
        string prompt,
        byte[] png,
        CancellationToken token)
    {
        var key = _credentials.Key;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("尚未配置百炼 API Key，请先到「设置」填写。");
        }

        var dataUrl = "data:image/png;base64," + Convert.ToBase64String(png);
        var payload = new
        {
            model,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "image_url", image_url = new { url = dataUrl } },
                        new { type = "text", text = prompt }
                    }
                }
            }
        };

        var url = _settings.Current.ApiBase.TrimEnd('/') + "/chat/completions";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await Http.SendAsync(request, token);
        var body = await response.Content.ReadAsStringAsync(token);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                _credentials.MarkExpired(true);
                throw new InvalidOperationException("百炼 API Key 已过期或无效，请到「设置」更新。");
            }

            throw new InvalidOperationException($"视觉模型返回 {(int)response.StatusCode}：{Trim(body)}");
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    private static string Trim(string text) =>
        text.Length <= 300 ? text : text[..300] + "...";
}
