using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Local;

/// <summary>
/// Maps the device network state. Auth routing must not depend on this service.
/// </summary>
public sealed class DeviceConnectivityService : IConnectivityService
{
    private ConnectivityStatus _status;
    private int _pending;

    public DeviceConnectivityService()
    {
        _status = Map(Connectivity.Current.NetworkAccess);
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    public ConnectivityStatus Status => _status;
    public bool IsOnline => _status is ConnectivityStatus.Online or ConnectivityStatus.Synced or ConnectivityStatus.Syncing;
    public int PendingCount => _pending;

    public event EventHandler<ConnectivityStatus>? StatusChanged;

    public void UpdateStatus(ConnectivityStatus status)
    {
        _status = status;
        StatusChanged?.Invoke(this, status);
    }

    public void IncrementPending()
    {
        _pending++;
        UpdateStatus(ConnectivityStatus.PendingSync);
    }

    public void DecrementPending(int count = 1)
    {
        _pending = Math.Max(0, _pending - count);
        if (_pending == 0)
            UpdateStatus(IsNetworkUp() ? ConnectivityStatus.Online : ConnectivityStatus.Offline);
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var next = Map(e.NetworkAccess);
        MainThread.BeginInvokeOnMainThread(() => UpdateStatus(next));
    }

    private static bool IsNetworkUp()
        => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    private static ConnectivityStatus Map(NetworkAccess access)
        => access == NetworkAccess.Internet
            ? ConnectivityStatus.Online
            : ConnectivityStatus.Offline;
}
