using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartLearner.App.Infrastructure;
using SmartLearner.App.Models;
using SmartLearner.App.Services;

namespace SmartLearner.App.ViewModels;

public partial class HomeViewModel : ObservableObject, IDisposable
{
    private static readonly string[] LunarMonths =
    {
        "正月", "二月", "三月", "四月", "五月", "六月",
        "七月", "八月", "九月", "十月", "冬月", "腊月"
    };

    private static readonly string[] Digits =
    {
        "一", "二", "三", "四", "五", "六", "七", "八", "九", "十"
    };

    private readonly SettingsService _settings;
    private readonly CourseService _courses;
    private readonly ZhihuCredentialService _credentials;
    private readonly ZhihuFeedService _feed;
    private readonly PreferenceService _preferences;

    [ObservableProperty]
    private string _dateText = string.Empty;

    [ObservableProperty]
    private string _lunarText = string.Empty;

    [ObservableProperty]
    private string _courseCountText = "0";

    [ObservableProperty]
    private string _keywordCountText = "0";

    [ObservableProperty]
    private string _noteCountText = "0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CourseActionText))]
    private bool _hasCourses;

    [ObservableProperty]
    private bool _isOffline;

    [ObservableProperty]
    private Visibility _emptyStateVisibility = Visibility.Visible;

    [ObservableProperty]
    private Visibility _noClassVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility _listVisibility = Visibility.Collapsed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZhihuUnconfiguredVisibility))]
    [NotifyPropertyChangedFor(nameof(HotEmptyVisibility))]
    [NotifyPropertyChangedFor(nameof(KeywordEmptyVisibility))]
    private bool _isZhihuConfigured;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZhihuExpiredVisibility))]
    [NotifyPropertyChangedFor(nameof(ZhihuBannerVisibility))]
    private bool _isZhihuExpired;

    [ObservableProperty]
    private string _zhihuStatusText = string.Empty;

    [ObservableProperty]
    private bool _isZhihuBusy;

    [ObservableProperty]
    private int _zhihuTabIndex;

    public ObservableCollection<Course> TodayCourses { get; } = new();

    public ObservableCollection<ZhihuArticle> HotArticles { get; } = new();

    public ObservableCollection<ZhihuArticle> KeywordArticles { get; } = new();

    public ObservableCollection<string> Preferences { get; } = new();

    /// <summary>右上角按钮文案：还没有课程时是「添加课程」，否则是「管理课程」。</summary>
    public string CourseActionText => HasCourses ? "管理课程" : "添加课程";

    public CourseService CourseService => _courses;

    public bool HasHotArticles => HotArticles.Count > 0;

    public bool HasKeywordArticles => KeywordArticles.Count > 0;

    public Visibility HotVisibility => HasHotArticles ? Visibility.Visible : Visibility.Collapsed;

    public Visibility KeywordVisibility => HasKeywordArticles ? Visibility.Visible : Visibility.Collapsed;

    public Visibility HotEmptyVisibility =>
        IsZhihuConfigured && !HasHotArticles ? Visibility.Visible : Visibility.Collapsed;

    public Visibility KeywordEmptyVisibility =>
        IsZhihuConfigured && Preferences.Count > 0 && !HasKeywordArticles
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ZhihuUnconfiguredVisibility =>
        IsZhihuConfigured ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ZhihuExpiredVisibility =>
        IsZhihuExpired ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ZhihuBannerVisibility =>
        IsZhihuExpired && (HasHotArticles || HasKeywordArticles)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreferenceEmptyVisibility =>
        Preferences.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PreferenceListVisibility =>
        Preferences.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public HomeViewModel(
        SettingsService settings,
        CourseService courses,
        ZhihuCredentialService credentials,
        ZhihuFeedService feed,
        PreferenceService preferences)
    {
        _settings = settings;
        _courses = courses;
        _credentials = credentials;
        _feed = feed;
        _preferences = preferences;

        var today = DateTime.Today;
        DateText = today.ToString("M月d日 dddd", new CultureInfo("zh-CN"));
        LunarText = BuildLunarText(today);

        _courses.Changed += OnCoursesChanged;
        _preferences.Changed += OnPreferencesChanged;
        RefreshPreferences();
        Refresh();

#if DEBUG
        if (Environment.GetEnvironmentVariable("SL_ZHIHU_TAB") == "1")
        {
            ZhihuTabIndex = 1;
        }
#endif
    }

    private void OnPreferencesChanged(object? sender, EventArgs e) => RefreshPreferences();

    private void RefreshPreferences()
    {
        Preferences.Clear();
        foreach (var keyword in _preferences.Keywords)
        {
            Preferences.Add(keyword);
        }

        KeywordCountText = Preferences.Count.ToString();
        OnPropertyChanged(nameof(PreferenceEmptyVisibility));
        OnPropertyChanged(nameof(PreferenceListVisibility));
        OnPropertyChanged(nameof(KeywordEmptyVisibility));
    }

    [RelayCommand]
    private async Task AddPreferenceAsync()
    {
        if (_preferences.Keywords.Count >= PreferenceService.MaxCount)
        {
            ZhihuStatusText = "最多只能添加 6 个偏好";
            return;
        }

        var dialog = new Views.InputDialog("添加推送偏好", "关键词，例如：RAG", string.Empty)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!_preferences.Add(dialog.ResultText))
        {
            ZhihuStatusText = "关键词为空、重复或已达上限";
            return;
        }

        _feed.ClearCache();
        await RefreshZhihuAsync();
    }

    [RelayCommand]
    private async Task RemovePreferenceAsync(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return;
        }

        _preferences.Remove(keyword);
        _feed.ClearCache();
        await RefreshZhihuAsync();
    }

    public AddCourseViewModel CreateAddCourseViewModel() => new(_courses);

    public void Refresh()
    {
        TodayCourses.Clear();
        foreach (var course in _courses.GetToday())
        {
            TodayCourses.Add(course);
        }

        HasCourses = _courses.Courses.Count > 0;
        CourseCountText = _courses.Courses.Count.ToString();

        EmptyStateVisibility = HasCourses ? Visibility.Collapsed : Visibility.Visible;
        ListVisibility = TodayCourses.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoClassVisibility = HasCourses && TodayCourses.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 每次进入首页都重新读取 Access Key：
    /// 有本周缓存先展示；没有缓存才拉取（每周一自动换新）。
    /// Key 失效时保留缓存并提示更新。
    /// </summary>
    public async Task RefreshZhihuAsync()
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("SL_ZHIHU_MOCK") == "1")
        {
            IsZhihuConfigured = true;
            IsZhihuExpired = true;
            ApplyFeed(BuildMockFeed(Preferences.Count > 0));
            ZhihuStatusText = "缓存 · 模拟数据";
            return;
        }
#endif

        _credentials.Load();
        IsZhihuConfigured = _credentials.HasKey;
        IsZhihuExpired = _credentials.IsExpired;
        KeywordCountText = _preferences.Keywords.Count.ToString();

        if (!IsZhihuConfigured)
        {
            ApplyFeed(null);
            ZhihuStatusText = string.Empty;
            return;
        }

        var cache = _feed.LoadCache();
        if (cache is not null)
        {
            ApplyFeed(cache);
            ZhihuStatusText = $"缓存 · {cache.FetchedAt:MM-dd HH:mm}";
            if (IsZhihuExpired)
            {
                return;
            }

            return;
        }

        if (IsZhihuExpired)
        {
            ApplyFeed(null);
            ZhihuStatusText = "Access Key 已过期，请到设置更新";
            return;
        }

        await FetchAsync();
    }

    [RelayCommand]
    private async Task RefreshZhihuNow()
    {
        _feed.ClearCache();
        await RefreshZhihuAsync();
    }

    private async Task FetchAsync()
    {
        try
        {
            IsZhihuBusy = true;
            ZhihuStatusText = "正在更新知乎内容…";

            var feed = await _feed.RefreshAsync(_preferences.Keywords, null, CancellationToken.None);
            ApplyFeed(feed);
            ZhihuStatusText = $"已更新 · {feed.FetchedAt:MM-dd HH:mm}";
        }
        catch (ZhihuAuthException)
        {
            _credentials.MarkExpired(true);
            IsZhihuExpired = true;
            ZhihuStatusText = "Access Key 已过期，请到设置更新";
            ApplyFeed(_feed.LoadCache());
        }
        catch (Exception ex)
        {
            Logger.Warn($"知乎推送更新失败：{ex.Message}");
            ZhihuStatusText = "更新失败，稍后重试";
        }
        finally
        {
            IsZhihuBusy = false;
        }
    }

    private void ApplyFeed(ZhihuFeed? feed)
    {
        HotArticles.Clear();
        KeywordArticles.Clear();

        if (feed is not null)
        {
            foreach (var article in feed.Hot)
            {
                HotArticles.Add(article);
            }

            foreach (var article in feed.KeywordItems)
            {
                KeywordArticles.Add(article);
            }
        }

        OnPropertyChanged(nameof(HasHotArticles));
        OnPropertyChanged(nameof(HasKeywordArticles));
        OnPropertyChanged(nameof(HotVisibility));
        OnPropertyChanged(nameof(KeywordVisibility));
        OnPropertyChanged(nameof(HotEmptyVisibility));
        OnPropertyChanged(nameof(KeywordEmptyVisibility));
        OnPropertyChanged(nameof(ZhihuBannerVisibility));
    }

    [RelayCommand]
    private void OpenArticle(ZhihuArticle? article)
    {
        if (article is null || string.IsNullOrWhiteSpace(article.Url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(article.Url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Warn($"打开知乎链接失败：{ex.Message}");
        }
    }

    private void OnCoursesChanged(object? sender, EventArgs e) => Refresh();

    /// <summary>把公历日期转成“农历十月初八”这类中文表述。</summary>
    private static string BuildLunarText(DateTime date)
    {
        try
        {
            var calendar = new ChineseLunisolarCalendar();
            var year = calendar.GetYear(date);
            var month = calendar.GetMonth(date);
            var day = calendar.GetDayOfMonth(date);
            var leapMonth = calendar.GetLeapMonth(year);

            var isLeap = leapMonth > 0 && month == leapMonth;
            var displayMonth = leapMonth > 0 && month >= leapMonth ? month - 1 : month;
            displayMonth = Math.Clamp(displayMonth, 1, 12);

            return $"农历{(isLeap ? "闰" : string.Empty)}{LunarMonths[displayMonth - 1]}{BuildLunarDay(day)}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string BuildLunarDay(int day)
    {
        return day switch
        {
            <= 0 => string.Empty,
            <= 10 => "初" + Digits[day - 1],
            < 20 => "十" + Digits[day - 11],
            20 => "二十",
            < 30 => "廿" + Digits[day - 21],
            _ => "三十"
        };
    }

    public void Dispose()
    {
        _courses.Changed -= OnCoursesChanged;
        _preferences.Changed -= OnPreferencesChanged;
    }

#if DEBUG
    private static ZhihuFeed BuildMockFeed(bool includeKeyword)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var feed = new ZhihuFeed
        {
            WeekStart = ZhihuFeedService.CurrentWeekStart(DateTime.Today).ToString("yyyy-MM-dd"),
            FetchedAt = DateTime.Now.AddHours(-3),
            Hot = new List<ZhihuArticle>
            {
                new()
                {
                    Title = "如何评价新一代 AI 编程助手？",
                    Url = "https://www.zhihu.com/question/1",
                    Summary = "从工程实践角度看，它确实改变了日常写代码的方式，但离“替代”还很远。",
                    Author = "张三",
                    VoteUp = 1280,
                    EditTime = now - 3600,
                    Source = "热榜"
                },
                new()
                {
                    Title = "为什么说 RAG 在垂直领域仍然值得做？",
                    Url = "https://www.zhihu.com/question/2",
                    Summary = "检索增强生成能显著降低幻觉，尤其是需要引用来源的学习场景。",
                    Author = "李四",
                    VoteUp = 860,
                    EditTime = now - 7200,
                    Source = "热榜"
                }
            }
        };

        if (includeKeyword)
        {
            feed.KeywordItems.Add(new ZhihuArticle
            {
                Title = "向量数据库选型指南",
                Url = "https://zhuanlan.zhihu.com/p/1",
                Summary = "从数据规模、延迟和维护成本三个维度对比常见方案。",
                Author = "王五",
                VoteUp = 320,
                EditTime = now - 86400,
                Source = "关键词",
                Keyword = "RAG"
            });
        }

        return feed;
    }
#endif
}
