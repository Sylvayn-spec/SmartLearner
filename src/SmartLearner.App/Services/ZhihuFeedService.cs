using System.IO;
using SmartLearner.App.Infrastructure;
using SmartLearner.App.Models;

namespace SmartLearner.App.Services;

/// <summary>
/// 知乎推送的周缓存：
/// - 每周一清除上一周的数据；
/// - 只保留最近 7 天的文章；
/// - 拉取失败时保留旧缓存，供首页降级展示。
/// </summary>
public sealed class ZhihuFeedService
{
    private readonly ZhihuApiClient _api;
    private readonly ZhihuCredentialService _credentials;

    public ZhihuFeedService(ZhihuApiClient api, ZhihuCredentialService credentials)
    {
        _api = api;
        _credentials = credentials;
    }

    public static DateTime CurrentWeekStart(DateTime today)
    {
        var offset = ((int)today.DayOfWeek + 6) % 7; // 周一 = 0
        return today.Date.AddDays(-offset);
    }

    /// <summary>读取本周缓存；跨周则直接清除并返回 null。</summary>
    public ZhihuFeed? LoadCache()
    {
        try
        {
            if (!File.Exists(AppPaths.ZhihuFeedCache))
            {
                return null;
            }

            var feed = JsonStore.Load<ZhihuFeed>(AppPaths.ZhihuFeedCache, () => null!);
            if (feed is null)
            {
                return null;
            }

            if (feed.WeekStart != CurrentWeekStart(DateTime.Today).ToString("yyyy-MM-dd"))
            {
                Logger.Info("检测到跨周，清除上周知乎推送缓存");
                ClearCache();
                return null;
            }

            return feed;
        }
        catch (Exception ex)
        {
            Logger.Warn($"读取知乎缓存失败，已清除：{ex.Message}");
            ClearCache();
            return null;
        }
    }

    public void ClearCache()
    {
        try
        {
            if (File.Exists(AppPaths.ZhihuFeedCache))
            {
                File.Delete(AppPaths.ZhihuFeedCache);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"清除知乎缓存失败：{ex.Message}");
        }
    }

    public async Task<ZhihuFeed> RefreshAsync(
        IReadOnlyList<string> keywords,
        IProgress<string>? progress,
        CancellationToken token)
    {
        var feed = new ZhihuFeed
        {
            WeekStart = CurrentWeekStart(DateTime.Today).ToString("yyyy-MM-dd"),
            FetchedAt = DateTime.Now
        };

        progress?.Report("正在拉取知乎热榜…");
        var hot = await _api.GetHotListAsync(10, token);
        foreach (var article in FilterRecent(hot))
        {
            article.Source = "热榜";
            feed.Hot.Add(article);
        }

        foreach (var keyword in keywords.Where(k => !string.IsNullOrWhiteSpace(k)).Take(6))
        {
            token.ThrowIfCancellationRequested();
            progress?.Report($"正在拉取关键词「{keyword}」…");

            List<ZhihuArticle> items;
            try
            {
                items = await _api.SearchAsync(keyword, 3, token);
            }
            catch (ZhihuAuthException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Warn($"关键词「{keyword}」拉取失败：{ex.Message}");
                continue;
            }

            foreach (var article in FilterRecent(items))
            {
                article.Source = "关键词";
                article.Keyword = keyword;
                feed.KeywordItems.Add(article);
            }

            await Task.Delay(250, token);
        }

        JsonStore.Save(AppPaths.ZhihuFeedCache, feed);
        return feed;
    }

    private static List<ZhihuArticle> FilterRecent(IEnumerable<ZhihuArticle> items)
    {
        var threshold = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
        return items.Where(a => a.EditTime <= 0 || a.EditTime >= threshold).ToList();
    }
}
