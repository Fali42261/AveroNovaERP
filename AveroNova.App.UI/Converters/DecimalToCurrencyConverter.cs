using System.Globalization;

namespace AveroNova.App.UI.Converters;

public class DecimalToCurrencyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal d)
        {
            string symbol = parameter as string ?? "$";
            return $"{symbol}{d:N2}";
        }
        return value?.ToString() ?? "$0.00";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => decimal.TryParse(value?.ToString()?.TrimStart('$'), out var d) ? d : 0m;
}
