using System.Windows;
using System.Windows.Controls;
using SmartLearner.App.Models;
using SmartLearner.App.Services;
using SmartLearner.App.ViewModels;

namespace SmartLearner.App.Views;

public partial class ManageCoursesWindow : Window
{
    private readonly CourseService _service;

    public ManageCoursesWindow(CourseService service)
    {
        InitializeComponent();
        _service = service;
        Reload();

        _service.Changed += OnCoursesChanged;
        Closed += (_, _) => _service.Changed -= OnCoursesChanged;
    }

    private void OnCoursesChanged(object? sender, EventArgs e) => Reload();

    private void Reload()
    {
        var list = _service.Courses
            .OrderBy(c => ((int)c.Day + 6) % 7)
            .ThenBy(c => c.Start)
            .ToList();

        CourseList.ItemsSource = list;
        CountText.Text = $"共 {list.Count} 门课程";
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AddCourseWindow(new AddCourseViewModel(_service))
        {
            Owner = this
        };

        dialog.ShowDialog();
        Reload();
    }

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Course course)
        {
            return;
        }

        var dialog = new AddCourseWindow(new AddCourseViewModel(_service, course))
        {
            Owner = this
        };

        dialog.ShowDialog();
        Reload();
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Course course)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"确定删除课程「{course.Name}」吗？\n\nDocuments 下的资料文件夹会保留。",
            "删除课程",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.OK)
        {
            return;
        }

        _service.Delete(course, deleteFolder: false);
        Reload();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
