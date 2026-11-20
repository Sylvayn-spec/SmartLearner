using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using SmartLearner.App.Models;

namespace SmartLearner.App.Services;

public sealed class ZhihuAuthException : Exception
{
    public ZhihuAuthException(string message) : base(message)
    {
    }
}

/// <summary>知乎开放平台：热榜与站内搜索。</summary>
public sealed class ZhihuApiClient
{
    private const string BaseUrl = "https://developer.zhihu.com";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly ZhihuCredentialService _credentials;

    public ZhihuApiClient(ZhihuCredentialService credentials) => _credentials = credentials;

    public async Task<List<ZhihuArticle>> GetHotListAsync(int limit, CancellationToken token)
    {
        using var document = await GetAsync($"/api/v1/content/hot_list?Limit={limit}", token);
        return ParseItems(document.RootElement);
    }

    public async Task<List<ZhihuArticle>> SearchAsync(string query, int count, CancellationToken token)
    {
        var url = $"/api/v1/content/zhihu_search?Query={Uri.EscapeDataString(query)}&Count={count}";
        using var document = await GetAsync(url, token);
        return ParseItems(document.RootElement);
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken token)
    {
        var key = _credentials.Key;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ZhihuAuthException("尚未配置 Access Key");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Add(
            "X-Request-Timestamp",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));

        using var response = await Http.SendAsync(request, token);
        var body = await response.Content.ReadAsStringAsync(token);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"知乎接口 HTTP {(int)response.StatusCode}");
        }

        var document = JsonDocument.Parse(body);
        var code = document.RootElement.TryGetProperty("Code", out var codeElement)
            ? codeElement.GetInt32()
            : -1;

        if (code == 20001)
        {
            document.Dispose();
            throw new ZhihuAuthException("Access Key 已失效，请更新");
        }

        if (code != 0)
        {
            var message = document.RootElement.TryGetProperty("Message", out var messageElement)
                ? messageElement.GetString()
                : $"知乎接口返回错误码 {code}";
            document.Dispose();
            throw new InvalidOperationException(message ?? "知乎接口返回错误");
        }

        return document;
    }

    private static List<ZhihuArticle> ParseItems(JsonElement root)
    {
        var result = new List<ZhihuArticle>();

        if (!root.TryGetProperty("Data", out var data) ||
            !data.TryGetProperty("Items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in items.EnumerateArray())
        {
            result.Add(new ZhihuArticle
            {
                Title = GetString(item, "Title"),
                Url = GetString(item, "Url"),
                Summary = GetString(item, "ContentText"),
                Author = GetString(item, "AuthorName"),
                VoteUp = GetInt(item, "VoteUpCount"),
                EditTime = GetLong(item, "EditTime")
            });
        }

        return result;
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int GetInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;

    private static long GetLong(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : 0;
}
