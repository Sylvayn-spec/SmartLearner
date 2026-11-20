using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using SmartLearner.App.Infrastructure;
using SmartLearner.App.Services;

namespace SmartLearner.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly SettingsService _settings;
    private readonly NetworkMonitor _network;
    private readonly ZhihuCredentialService _credentials;
    private readonly BailianCredentialService _bailian;

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    [ObservableProperty]
    private ObservableObject? _currentPage;

    [ObservableProperty]
    private string _pageTitle = "首页";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NetworkText))]
    private bool _isOnline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SecretText))]
    [NotifyPropertyChangedFor(nameof(HasAccessSecret))]
    private bool _secretConfigured;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeIcon))]
    private bool _isDarkMode;

    public ObservableCollection<NavItem> NavItems { get; }

    public HomeViewModel HomePage { get; }

    public CourseViewModel CoursePage { get; }

    public OrganizeViewModel OrganizePage { get; }

    public SettingsViewModel SettingsPage { get; }

    public UiState Ui { get; }

    [ObservableProperty]
    private bool _isHomePage = true;

    [ObservableProperty]
    private bool _isCoursePage;

    [ObservableProperty]
    private bool _isOrganizePage;

    [ObservableProperty]
    private bool _isSettingsPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BailianText))]
    private bool _bailianConfigured;

    public string BailianText => !BailianConfigured
        ? "百炼未配置"
        : (_bailian.IsExpired ? "百炼已过期" : "百炼已配置");

    public bool BailianOnline => BailianConfigured && !_bailian.IsExpired;

    public string NetworkText => IsOnline ? "在线" : "离线";

    public string SecretText => !SecretConfigured
        ? "知乎未配置"
        : (_credentials.IsExpired ? "知乎已过期" : "知乎已配置");

    public bool HasAccessSecret => SecretConfigured;

    public PackIconKind ThemeIcon => IsDarkMode ? PackIconKind.WeatherNight : PackIconKind.WeatherSunny;

    public MainViewModel(
        SettingsService settings,
        NetworkMonitor network,
        UiState ui,
        HomeViewModel home,
        CourseViewModel course,
        OrganizeViewModel organize,
        SettingsViewModel settingsPage,
        ZhihuCredentialService credentials,
        BailianCredentialService bailian)
    {
        _settings = settings;
        _network = network;
        _credentials = credentials;
        _bailian = bailian;
        Ui = ui;

        HomePage = home;
        CoursePage = course;
        OrganizePage = organize;
        SettingsPage = settingsPage;

        NavItems = new ObservableCollection<NavItem>
        {
            new() { Label = "首页", Icon = PackIconKind.HomeVariant, Page = home },
            new() { Label = "课程", Icon = PackIconKind.School, Page = course },
            new() { Label = "整理", Icon = PackIconKind.FileDocumentEdit, Page = organize },
            new() { Label = "设置", Icon = PackIconKind.Cog, Page = settingsPage }
        };

        IsOnline = network.IsOnline;
        IsDarkMode = settings.Current.IsDarkMode;
        SecretConfigured = credentials.HasKey;
        RefreshBailian();

        network.OnlineChanged += OnOnlineChanged;
        settings.Changed += OnSettingsChanged;
        credentials.Changed += OnCredentialsChanged;
        bailian.Changed += OnBailianChanged;
        ui.NavigationRequested += OnNavigationRequested;

#if DEBUG
        // 仅调试：用于截图/自动化验证，指定启动页（首页/课程/整理/设置）。
        var startPage = Environment.GetEnvironmentVariable("SL_START_PAGE");
        if (!string.IsNullOrWhiteSpace(startPage))
        {
            var target = NavItems.FirstOrDefault(n => n.Label == startPage);
            if (target is not null)
            {
                SelectedNavItem = target;
                return;
            }
        }
#endif

        SelectedNavItem = NavItems[0];
    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value is null)
        {
            return;
        }

        CurrentPage = value.Page;
        PageTitle = value.Label;

        // 切页时退出阅读/整理的全屏，避免全屏状态带到别的页面
        Ui.ExitReaderFullscreen();

        IsHomePage = ReferenceEquals(value.Page, HomePage);
        IsCoursePage = ReferenceEquals(value.Page, CoursePage);
        IsOrganizePage = ReferenceEquals(value.Page, OrganizePage);
        IsSettingsPage = ReferenceEquals(value.Page, SettingsPage);

        if (IsHomePage)
        {
            _ = HomePage.RefreshZhihuAsync();
        }
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        _settings.SetDarkMode(IsDarkMode);
        ThemeService.Apply(IsDarkMode);
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var target = NavItems.FirstOrDefault(n => n.Label == "设置");
        if (target is not null)
        {
            SelectedNavItem = target;
        }
    }

    private void OnOnlineChanged(object? sender, bool online)
    {
        IsOnline = online;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        SecretConfigured = _credentials.HasKey;
        IsDarkMode = _settings.Current.IsDarkMode;
    }

    private void OnCredentialsChanged(object? sender, EventArgs e)
    {
        SecretConfigured = _credentials.HasKey;
        OnPropertyChanged(nameof(SecretText));
    }

    private void OnBailianChanged(object? sender, EventArgs e) => RefreshBailian();

    private void RefreshBailian()
    {
        BailianConfigured = _bailian.HasKey;
        OnPropertyChanged(nameof(BailianText));
        OnPropertyChanged(nameof(BailianOnline));
    }

    private void OnNavigationRequested(object? sender, string label)
    {
        var target = NavItems.FirstOrDefault(n => n.Label == label);
        if (target is not null)
        {
            SelectedNavItem = target;
        }
    }

    public void Dispose()
    {
        _network.OnlineChanged -= OnOnlineChanged;
        _settings.Changed -= OnSettingsChanged;
        _credentials.Changed -= OnCredentialsChanged;
        Ui.NavigationRequested -= OnNavigationRequested;
    }
}
