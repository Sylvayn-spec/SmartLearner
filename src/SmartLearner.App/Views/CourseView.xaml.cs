using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SmartLearner.App.Infrastructure;
using SmartLearner.App.Models;
using SmartLearner.App.ViewModels;

namespace SmartLearner.App.Views;

public partial class CourseView : UserControl
{
    private CourseViewModel? _viewModel;
    private string? _pendingHtml;

    public CourseView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
        ChatScroll.ScrollChanged += OnChatScrollChanged;
    }

    private void OnManageChatsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CourseViewModel viewModel)
        {
            return;
        }

        var window = new ManageChatsWindow(viewModel)
        {
            Owner = Window.GetWindow(this)
        };

        window.ShowDialog();
    }

    private void OnDeleteSessionClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CourseViewModel viewModel)
        {
            return;
        }

        var session = (sender as FrameworkElement)?.DataContext as ChatSession;
        if (session is null && (sender as FrameworkElement)?.DataContext is string title)
        {
            session = viewModel.Sessions.FirstOrDefault(item => item.Title == title);
        }

        if (session is not null)
        {
            viewModel.DeleteSession(session);
        }
    }

    private void OnChatScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange == 0 ||
            Environment.GetEnvironmentVariable("SL_DEBUG_CHATTOP") == "1")
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            ChatScroll.UpdateLayout();
            ChatScroll.ScrollToVerticalOffset(ChatScroll.ScrollableHeight);
        }), DispatcherPriority.ContextIdle);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await PdfView.EnsureCoreWebView2Async();
            if (PdfView.CoreWebView2 is not null)
            {
                PdfView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                PdfView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                PdfView.CoreWebView2.ContainsFullScreenElementChanged += OnContainsFullScreenChanged;
            }

            ApplyPdfSource();

            if (!string.IsNullOrEmpty(_pendingHtml) && PdfView.CoreWebView2 is not null)
            {
                PdfView.NavigateToString(_pendingHtml);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("WebView2 初始化失败，PDF 预览不可用", ex);
        }

#if DEBUG
        if (Environment.GetEnvironmentVariable("SL_DEBUG_CHATTOP") == "1")
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                Dispatcher.Invoke(() =>
                {
                    ChatScroll.UpdateLayout();
                    ChatScroll.ScrollToTop();
                });
            });
        }
#endif

#if DEBUG
        if (Environment.GetEnvironmentVariable("SL_DEBUG_HIDE_COMBO") == "1")
        {
            SessionPicker.Visibility = Visibility.Collapsed;
        }
#endif

#if DEBUG
        if (Environment.GetEnvironmentVariable("SL_DEBUG_FULLSCREEN") == "1" &&
            DataContext is CourseViewModel viewModel)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                Dispatcher.Invoke(() => viewModel.Ui.IsReaderFullscreen = true);
            });
        }
#endif
    }

    /// <summary>
    /// Chromium 自带的“全屏”只是把 PDF 元素撑满 WebView2，应用窗口不会变。
    /// 这里拦截它，改成应用内全屏（隐藏顶栏/侧栏/文件列表），让阅读区真正占满窗口。
    /// </summary>
    private void OnContainsFullScreenChanged(object? sender, object e)
    {
        if (PdfView.CoreWebView2?.ContainsFullScreenElement != true)
        {
            return;
        }

        // 让 Chromium 退出它自己的全屏（可能失败，PDF 内置查看器不保证支持脚本）。
        _ = PdfView.CoreWebView2.ExecuteScriptAsync("document.exitFullscreen && document.exitFullscreen();");

        if (_viewModel is not null)
        {
            _viewModel.Ui.IsReaderFullscreen = true;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (PdfView.CoreWebView2 is not null)
        {
            PdfView.CoreWebView2.ContainsFullScreenElementChanged -= OnContainsFullScreenChanged;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.ChatMessages.CollectionChanged -= OnChatCollectionChanged;
            _viewModel = null;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.ChatMessages.CollectionChanged -= OnChatCollectionChanged;
        }

        _viewModel = DataContext as CourseViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.ChatMessages.CollectionChanged += OnChatCollectionChanged;
        }

        ApplyPdfSource();
        ApplyHtml();
    }

    private void OnChatCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Environment.GetEnvironmentVariable("SL_DEBUG_CHATTOP") == "1")
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            ChatScroll.UpdateLayout();
            ChatScroll.ScrollToVerticalOffset(ChatScroll.ScrollableHeight);
        }), DispatcherPriority.Loaded);
    }

    private void OnChatKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return;
        }

        e.Handled = true;
        if (DataContext is CourseViewModel viewModel && viewModel.SendChatCommand.CanExecute(null))
        {
            viewModel.SendChatCommand.Execute(null);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CourseViewModel.PdfUri))
        {
            ApplyPdfSource();
        }
        else if (e.PropertyName == nameof(CourseViewModel.HtmlContent))
        {
            ApplyHtml();
        }
    }

    /// <summary>Markdown 渲染结果的展示（与 PDF 共用同一个 WebView2）。</summary>
    private void ApplyHtml()
    {
        var html = _viewModel?.HtmlContent;
        if (string.IsNullOrWhiteSpace(html) || _viewModel?.Mode != PreviewMode.Rendered)
        {
            return;
        }

        _pendingHtml = html;

        if (PdfView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            PdfView.NavigateToString(html);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Markdown 预览失败：{ex.Message}");
        }
    }

    /// <summary>
    /// WebView2 的 Source 不接受 null，所以完全由代码控制：
    /// 只有拿到非空地址时才赋值，切页/清空一律不动它。
    /// </summary>
    private void ApplyPdfSource()
    {
        if (PdfView.CoreWebView2 is null)
        {
            // 尚未初始化，等 OnLoaded 完成后再设置。
            return;
        }

        var address = _viewModel?.PdfUri;
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        try
        {
            PdfView.Source = new Uri(address);
        }
        catch (Exception ex)
        {
            Logger.Warn($"设置 PDF 地址失败：{address}，{ex.Message}");
        }
    }

    private void OnEntryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is CourseViewModel viewModel &&
            viewModel.SelectedEntry is { IsFolder: true } folder)
        {
            viewModel.EnterFolder(folder);
        }
    }
}
