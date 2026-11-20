using System.Text.Json.Serialization;

namespace SmartLearner.App.Models;

/// <summary>
/// 知乎推送文章（热榜与关键词统一结构）。
/// </summary>
public sealed class ZhihuArticle
{
    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public int VoteUp { get; set; }

    /// <summary>秒级时间戳。</summary>
    public long EditTime { get; set; }

    /// <summary>来源：热榜 / 关键词。</summary>
    public string Source { get; set; } = string.Empty;

    public string Keyword { get; set; } = string.Empty;

    [JsonIgnore]
    public string TimeText => EditTime <= 0
        ? string.Empty
        : DateTimeOffset.FromUnixTimeSeconds(EditTime).ToLocalTime().ToString("M月d日");

    [JsonIgnore]
    public string MetaText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Source))
            {
                parts.Add(Source);
            }

            if (!string.IsNullOrWhiteSpace(Keyword))
            {
                parts.Add("#" + Keyword);
            }

            if (!string.IsNullOrWhiteSpace(Author))
            {
                parts.Add(Author);
            }

            if (VoteUp > 0)
            {
                parts.Add($"{VoteUp} 赞");
            }

            if (!string.IsNullOrWhiteSpace(TimeText))
            {
                parts.Add(TimeText);
            }

            return string.Join(" · ", parts);
        }
    }
}

/// <summary>一周的知乎推送缓存。</summary>
public sealed class ZhihuFeed
{
    /// <summary>本周一的日期（yyyy-MM-dd），用于判断是否跨周。</summary>
    public string WeekStart { get; set; } = string.Empty;

    public DateTime FetchedAt { get; set; }

    public List<ZhihuArticle> Hot { get; set; } = new();

    public List<ZhihuArticle> KeywordItems { get; set; } = new();
}

/// <summary>AccessKey/zhihu.json 的内容。AccessKey 本身用 DPAPI 加密后存放。</summary>
public sealed class ZhihuCredential
{
    public string? AccessKey { get; set; }

    public DateTime SavedAt { get; set; }

    /// <summary>最近一次调用是否被判为失效（20001）。</summary>
    public bool Expired { get; set; }
}
