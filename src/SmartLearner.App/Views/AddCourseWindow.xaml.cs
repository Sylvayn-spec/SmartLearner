using System.Windows;
using SmartLearner.App.ViewModels;

namespace SmartLearner.App.Views;

public partial class AddCourseWindow : Window
{
    private readonly AddCourseViewModel _viewModel;

    public AddCourseWindow(AddCourseViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.TrySave())
        {
            DialogResult = true;
            Close();
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
