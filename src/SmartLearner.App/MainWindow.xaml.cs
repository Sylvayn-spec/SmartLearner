using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SmartLearner.App.Infrastructure;
using SmartLearner.App.Services;
using SmartLearner.App.ViewModels;

namespace SmartLearner.App;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VkEscape = 0x1B;

    private readonly MainViewModel _viewModel;
    private readonly UiState _ui;
    private readonly DispatcherTimer _escapeTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };

    public MainWindow(MainViewModel viewModel, UiState ui)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _ui = ui;
        DataContext = viewModel;

        SourceInitialized += (_, _) => FitToWorkArea();
        PreviewKeyDown += OnPreviewKeyDown;
        _escapeTimer.Tick += OnEscapeTimerTick;
        _ui.PropertyChanged += OnUiStateChanged;
        Closed += OnClosed;

        ApplyChrome();
    }

    /// <summary>
    /// WebView2 是独立 HWND，焦点在 PDF 里时 WPF 收不到 Esc。
    /// 全屏期间轮询系统按键状态，保证任何焦点下 Esc 都能退出。
    /// </summary>
    private void OnEscapeTimerTick(object? sender, EventArgs e)
    {
        if (_ui.IsReaderFullscreen && (GetAsyncKeyState(VkEscape) & 0x8000) != 0)
        {
            _ui.ExitReaderFullscreen();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _escapeTimer.Stop();
        _ui.PropertyChanged -= OnUiStateChanged;
        _viewModel.Dispose();
    }

    private void OnUiStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UiState.IsReaderFullscreen))
        {
            ApplyChrome();
        }
    }

    /// <summary>阅读器全屏时隐藏顶栏与侧栏，让内容占满整个窗口。</summary>
    private void ApplyChrome()
    {
        var full = _ui.IsReaderFullscreen;
        Logger.Info($"ReaderFullscreen={full}");

        HeaderRow.Height = full ? new GridLength(0) : new GridLength(64);
        SidebarColumn.Width = full ? new GridLength(0) : new GridLength(212);
        HeaderBar.Visibility = full ? Visibility.Collapsed : Visibility.Visible;
        SidebarBar.Visibility = full ? Visibility.Collapsed : Visibility.Visible;
        ContentHost.Margin = full ? new Thickness(0) : new Thickness(26, 22, 26, 22);

        if (full)
        {
            _escapeTimer.Start();
        }
        else
        {
            _escapeTimer.Stop();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _ui.IsReaderFullscreen)
        {
            _ui.ExitReaderFullscreen();
            e.Handled = true;
        }
    }

    /// <summary>
    /// 按工作区约束窗口尺寸。高分屏下 DIP 与像素不等，
    /// 直接用固定 Width 会把窗口撑出屏幕。
    /// </summary>
    private void FitToWorkArea()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var work = SystemParameters.WorkArea;

        // SystemParameters.WorkArea 返回的是物理像素，而 Width/Left 是 DIP，必须换算。
        var scaleX = dpi.DpiScaleX <= 0 ? 1 : dpi.DpiScaleX;
        var scaleY = dpi.DpiScaleY <= 0 ? 1 : dpi.DpiScaleY;
        var workWidthDip = work.Width / scaleX;
        var workHeightDip = work.Height / scaleY;

        Logger.Info(
            $"DPI={scaleX:0.##} WorkArea={work.Width:0}x{work.Height:0}px " +
            $"({workWidthDip:0}x{workHeightDip:0}dip) Window(Before)={Width:0}x{Height:0}");

        var maxWidth = Math.Max(MinWidth, workWidthDip - 24);
        var maxHeight = Math.Max(MinHeight, workHeightDip - 24);

        Width = Math.Min(Width, maxWidth);
        Height = Math.Min(Height, maxHeight);
        Left = work.Left / scaleX + (workWidthDip - Width) / 2;
        Top = work.Top / scaleY + (workHeightDip - Height) / 2;

        Logger.Info($"Window(After)={Width:0}x{Height:0} at {Left:0},{Top:0}");
    }
}
