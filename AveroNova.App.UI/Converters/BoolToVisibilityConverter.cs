using System.Globalization;

namespace AveroNova.App.UI.Converters;

/// <summary>Converts bool → IsVisible. Pass parameter="invert" to reverse.</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool boolValue = value is bool b && b;
        bool invert    = parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase);
        return invert ? !boolValue : boolValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value;
}
