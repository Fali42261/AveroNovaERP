using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockConnectivityService : IConnectivityService
{
    private ConnectivityStatus _status = ConnectivityStatus.Online;
    private int                _pending;

    public ConnectivityStatus Status      => _status;
    public bool               IsOnline    => _status == ConnectivityStatus.Online || _status == ConnectivityStatus.Synced;
    public int                PendingCount => _pending;

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
        if (_pending == 0) UpdateStatus(ConnectivityStatus.Synced);
    }
}
