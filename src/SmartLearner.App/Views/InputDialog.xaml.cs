using System.Windows;

namespace SmartLearner.App.Views;

public partial class InputDialog : Window
{
    public string ResultText => InputBox.Text.Trim();

    public InputDialog(string title, string label, string initialText = "")
    {
        InitializeComponent();
        Title = title;
        LabelText.Text = label;
        InputBox.Text = initialText;
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ResultText))
        {
            LabelText.Text = "名称不能为空";
            return;
        }

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
