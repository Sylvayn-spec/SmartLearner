using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SmartLearner.App.Infrastructure;
using SmartLearner.App.Services;
using SmartLearner.App.ViewModels;

namespace SmartLearner.App;

public partial class App : Application
{
    private ServiceProvider? _provider;
    private NetworkMonitor? _network;
    private SettingsService? _settings;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // 1. 先确保默认目录可用，再读配置（配置文件固定在默认目录）
        AppPaths.Initialize(null);
        _settings = new SettingsService();
        _settings.Load();

        // 2. 依据配置切到真正的数据目录
        AppPaths.Initialize(_settings.Current.DataRoot);
        Logger.Initialize(AppPaths.Logs);
        Logger.Info("Smart Learner 启动");

        // WebView2 的用户数据目录放进 SmartLearner，避免污染安装目录。
        var webViewData = Path.Combine(AppPaths.Cache, "webview2");
        Directory.CreateDirectory(webViewData);
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webViewData);

        // 3. 应用主题（在任何窗口创建之前）
        ThemeService.Apply(_settings.Current.IsDarkMode);

        // 3.1 知乎 Access Key 独立存储到 AccessKey/zhihu.json，并迁移旧配置
        var credentials = new ZhihuCredentialService();
        credentials.Load();
        var legacyZhihuKey = Dpapi.Unprotect(_settings.Current.ProtectedAccessSecret);
        if (!string.IsNullOrWhiteSpace(legacyZhihuKey))
        {
            credentials.ImportLegacy(legacyZhihuKey);
            _settings.Current.ProtectedAccessSecret = null;
            _settings.Save();
        }

#if DEBUG
        var debugKey = Environment.GetEnvironmentVariable("SL_DEBUG_ZHIHU_KEY");
        if (!string.IsNullOrWhiteSpace(debugKey))
        {
            credentials.SetKey(debugKey);
        }
#endif

        // 3.2 推送偏好独立存储到 Preference/preference.json，并迁移旧关键词
        var preferences = new PreferenceService();
        preferences.Load();
        preferences.ImportLegacy(_settings.Current.Keywords);

        // 3.3 百炼 Key 独立存储到 AccessKey/bailian.json
        var bailian = new BailianCredentialService();
        bailian.Load();
        var legacyBailianKey = Dpapi.Unprotect(_settings.Current.ProtectedBailianKey);
        if (!string.IsNullOrWhiteSpace(legacyBailianKey))
        {
            bailian.ImportLegacy(legacyBailianKey);
            _settings.Current.ProtectedBailianKey = null;
            _settings.Save();
        }

#if DEBUG
        var debugBailianKey = Environment.GetEnvironmentVariable("SL_DEBUG_BAILIAN_KEY");
        if (!string.IsNullOrWhiteSpace(debugBailianKey))
        {
            bailian.SetKey(debugBailianKey);
        }
#endif

        // 4. 网络探测：不阻塞启动，失败也不退出
        _network = new NetworkMonitor();
        _network.Start();

        // 5. 组装依赖
        var services = new ServiceCollection();
        services.AddSingleton(_settings);
        services.AddSingleton(_network);
        services.AddSingleton<UiState>();
        services.AddSingleton(credentials);
        services.AddSingleton(bailian);
        services.AddSingleton(preferences);
        services.AddSingleton<ZhihuApiClient>();
        services.AddSingleton<ZhihuFeedService>();
        services.AddSingleton<CourseService>();
        services.AddSingleton<DocumentService>();
        services.AddSingleton<BailianClient>();
        services.AddSingleton<OrganizeService>();
        services.AddSingleton<KnowledgeBaseService>();
        services.AddSingleton<ChatHistoryService>();
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<CourseViewModel>();
        services.AddSingleton<OrganizeViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        _provider = services.BuildServiceProvider();

        var window = _provider.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info("Smart Learner 退出");
        _network?.Dispose();
        _provider?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error("UI 线程未处理异常", e.Exception);
        MessageBox.Show(
            $"发生了未预期的错误，已记录到日志。\n\n{e.Exception.Message}",
            "Smart Learner",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Logger.Error("后台线程未处理异常", ex);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error("未观察的任务异常", e.Exception);
        e.SetObserved();
    }
}
