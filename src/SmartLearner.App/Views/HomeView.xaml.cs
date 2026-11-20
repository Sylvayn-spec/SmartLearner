using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SmartLearner.App.ViewModels;

namespace SmartLearner.App.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();

        // 初始焦点若落在卡片内的按钮上，WPF 会把整页滚下去；加载完成后回到顶部。
        Loaded += (_, _) => Dispatcher.BeginInvoke(
            new Action(() => PageScroll.ScrollToTop()),
            DispatcherPriority.ContextIdle);

#if DEBUG
        Loaded += OnDebugLoaded;
        Loaded += OnDebugScrollLoaded;
#endif
    }

#if DEBUG
    private bool _debugScrollApplied;

    /// <summary>调试用：SL_DEBUG_SCROLL=偏移量，滚到指定位置便于截图。</summary>
    private void OnDebugScrollLoaded(object sender, RoutedEventArgs e)
    {
        if (_debugScrollApplied ||
            !int.TryParse(Environment.GetEnvironmentVariable("SL_DEBUG_SCROLL"), out var offset))
        {
            return;
        }

        _debugScrollApplied = true;
        Dispatcher.BeginInvoke(
            new Action(() => PageScroll.ScrollToVerticalOffset(offset)),
            DispatcherPriority.ContextIdle);
    }
#endif

    private void OnAddCourseClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel)
        {
            return;
        }

        var dialog = new AddCourseWindow(viewModel.CreateAddCourseViewModel())
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            viewModel.Refresh();
        }
    }

    /// <summary>右上角按钮：没有课程时添加，已有课程时进入管理。</summary>
    private void OnCourseActionClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel)
        {
            return;
        }

        if (!viewModel.HasCourses)
        {
            OnAddCourseClick(sender, e);
            return;
        }

        var manager = new ManageCoursesWindow(viewModel.CourseService)
        {
            Owner = Window.GetWindow(this)
        };

        manager.ShowDialog();
        viewModel.Refresh();
    }

    /// <summary>首页「去设置」：跳到设置页配置知乎 Access Key。</summary>
    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this)?.DataContext is not MainViewModel main)
        {
            return;
        }

        var settings = main.NavItems.FirstOrDefault(n => n.Label == "设置");
        if (settings is not null)
        {
            main.SelectedNavItem = settings;
        }
    }

#if DEBUG
    private bool _debugDialogShown;

    /// <summary>调试用：SL_DEBUG_DIALOG=add-course / manage-courses 时自动弹窗，便于截图验证。</summary>
    private void OnDebugLoaded(object sender, RoutedEventArgs e)
    {
        if (_debugDialogShown)
        {
            return;
        }

        var mode = Environment.GetEnvironmentVariable("SL_DEBUG_DIALOG");
        if (mode is not ("add-course" or "manage-courses"))
        {
            return;
        }

        _debugDialogShown = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (mode == "manage-courses" && DataContext is HomeViewModel vm && vm.HasCourses)
            {
                var manager = new ManageCoursesWindow(vm.CourseService)
                {
                    Owner = Window.GetWindow(this)
                };
                manager.ShowDialog();
            }
            else
            {
                OnAddCourseClick(this, new RoutedEventArgs());
            }
        }), DispatcherPriority.Background);
    }
#endif
}
