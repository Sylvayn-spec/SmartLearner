using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SmartLearner.App.Infrastructure;
using SmartLearner.App.ViewModels;

namespace SmartLearner.App.Views;

public partial class OrganizeView : UserControl
{
    private OrganizeViewModel? _viewModel;
    private string? _pendingHtml;

    public OrganizeView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await HtmlView.EnsureCoreWebView2Async();
            if (HtmlView.CoreWebView2 is not null)
            {
                HtmlView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                HtmlView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            }

            if (!string.IsNullOrEmpty(_pendingHtml))
            {
                HtmlView.NavigateToString(_pendingHtml);
            }

#if DEBUG
            if (Environment.GetEnvironmentVariable("SL_ORGANIZE_MOCK") == "1" && _viewModel is not null)
            {
                _viewModel.FileName = "示例课件.pdf";
                _ = _viewModel.GenerateCommand.ExecuteAsync(null);
            }

            if (Environment.GetEnvironmentVariable("SL_DEBUG_FULLSCREEN") == "1" && _viewModel is not null)
            {
                var viewModel = _viewModel;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1500);
                    Dispatcher.Invoke(() => viewModel.Ui.IsReaderFullscreen = true);
                });
            }
#endif
        }
        catch (Exception ex)
        {
            Logger.Error("整理预览初始化失败", ex);
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.HtmlRendered -= OnHtmlRendered;
        }

        _viewModel = DataContext as OrganizeViewModel;
        if (_viewModel is not null)
        {
            _viewModel.HtmlRendered += OnHtmlRendered;
        }
    }

    private void OnHtmlRendered(object? sender, string html)
    {
        _pendingHtml = html;

        if (HtmlView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            HtmlView.NavigateToString(html);
        }
        catch (Exception ex)
        {
            Logger.Warn($"预览渲染失败：{ex.Message}");
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.HtmlRendered -= OnHtmlRendered;
            _viewModel = null;
        }
    }
}
