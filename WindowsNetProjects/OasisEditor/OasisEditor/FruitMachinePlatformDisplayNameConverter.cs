using System.Globalization;
using System.Windows.Data;

namespace OasisEditor;

public sealed class FruitMachinePlatformDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is FruitMachinePlatformType.MaygayM1 ? "Maygay M1" : value?.ToString() ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
