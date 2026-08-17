using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Networking;

namespace AveroNova.App.UI.Services;

/// <summary>
/// Single connectivity probe using MAUI networking APIs.
/// </summary>
public sealed class MauiConnectivityService : IConnectivityService
{
    private ConnectivityStatus _status = ConnectivityStatus.Online;
    private int _pending;

    public MauiConnectivityService()
    {
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
        _status = Map(Connectivity.NetworkAccess);
    }

    public ConnectivityStatus Status => _status;
    public bool IsOnline =>
        _status is ConnectivityStatus.Online or ConnectivityStatus.Synced or ConnectivityStatus.Syncing;
    public int PendingCount => _pending;

    public event EventHandler<ConnectivityStatus>? StatusChanged;

    public void UpdateStatus(ConnectivityStatus status)
    {
        if (_status == status) return;
        _status = status;
        StatusChanged?.Invoke(this, status);
    }

    public void IncrementPending()
    {
        Interlocked.Increment(ref _pending);
        if (_status != ConnectivityStatus.Offline)
            UpdateStatus(ConnectivityStatus.PendingSync);
    }

    public void DecrementPending(int count = 1)
    {
        var next = Math.Max(0, _pending - Math.Max(1, count));
        Interlocked.Exchange(ref _pending, next);
        if (next == 0 && IsNetworkAvailable())
            UpdateStatus(ConnectivityStatus.Synced);
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        => UpdateStatus(Map(e.NetworkAccess));

    private static bool IsNetworkAvailable()
        => Connectivity.NetworkAccess == NetworkAccess.Internet;

    private static ConnectivityStatus Map(NetworkAccess access)
        => access == NetworkAccess.Internet ? ConnectivityStatus.Online : ConnectivityStatus.Offline;
}
