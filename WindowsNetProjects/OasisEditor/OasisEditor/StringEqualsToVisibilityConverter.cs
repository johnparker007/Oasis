using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OasisEditor;

public sealed class StringEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var left = value?.ToString() ?? string.Empty;
        var right = parameter?.ToString() ?? string.Empty;
        return string.Equals(left, right, StringComparison.Ordinal) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
