using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SmartLearner.App.Infrastructure;

/// <summary>true → Collapsed，false → Visible。</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}

/// <summary>宽度减去一个偏移量，用于让气泡自适应容器宽度。</summary>
public sealed class WidthMinusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var width = value is double d ? d : 0;
        var offset = 100.0;

        if (parameter is string text &&
            double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            offset = parsed;
        }

        return Math.Max(120, width - offset);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
