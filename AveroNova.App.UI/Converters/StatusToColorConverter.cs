using System.Globalization;
using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Converters;

/// <summary>Maps ConnectivityStatus → Color for the status indicator dot.</summary>
public class StatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ConnectivityStatus.Online     => Color.FromArgb("#10B981"),
            ConnectivityStatus.Offline    => Color.FromArgb("#EF4444"),
            ConnectivityStatus.Syncing    => Color.FromArgb("#3B82F6"),
            ConnectivityStatus.Synced     => Color.FromArgb("#10B981"),
            ConnectivityStatus.SyncFailed => Color.FromArgb("#EF4444"),
            ConnectivityStatus.PendingSync=> Color.FromArgb("#F59E0B"),
            _                             => Color.FromArgb("#9CA3AF")
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => ConnectivityStatus.Online;
}
