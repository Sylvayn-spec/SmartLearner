using System.Windows;
using System.Windows.Controls;
using SmartLearner.App.ViewModels;

namespace SmartLearner.App.Views;

public partial class ManageChatsWindow : Window
{
    private readonly CourseViewModel _viewModel;

    public ManageChatsWindow(CourseViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var session in _viewModel.Sessions)
        {
            session.IsSelected = true;
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        var selected = _viewModel.Sessions.Where(session => session.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("请先勾选要删除的对话。", "批量删除", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"确定删除选中的 {selected.Count} 个对话吗？本地聊天记录和对话 ID 会一并删除。",
            "批量删除对话",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
        {
            return;
        }

        _viewModel.DeleteSessions(selected);
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
