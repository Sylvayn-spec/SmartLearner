using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartLearner.App.Services;

/// <summary>
/// 全局界面状态：阅读器全屏等跨页面共享的 UI 状态。
/// </summary>
public sealed partial class UiState : ObservableObject
{
    [ObservableProperty]
    private bool _isReaderFullscreen;

    /// <summary>请求切换到某个导航页（首页/课程/整理/设置）。</summary>
    public event EventHandler<string>? NavigationRequested;

    public void RequestNavigation(string label) => NavigationRequested?.Invoke(this, label);

    public void ToggleReaderFullscreen() => IsReaderFullscreen = !IsReaderFullscreen;

    public void ExitReaderFullscreen() => IsReaderFullscreen = false;
}
