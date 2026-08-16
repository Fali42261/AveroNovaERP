using System.Globalization;
using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Converters;

public class StatusToLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ConnectivityStatus.Online      => "Online",
            ConnectivityStatus.Offline     => "Offline",
            ConnectivityStatus.Syncing     => "Syncing",
            ConnectivityStatus.Synced      => "Synced",
            ConnectivityStatus.SyncFailed  => "Sync Failed",
            ConnectivityStatus.PendingSync => "Pending",
            _                              => "Unknown"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => ConnectivityStatus.Online;
}
