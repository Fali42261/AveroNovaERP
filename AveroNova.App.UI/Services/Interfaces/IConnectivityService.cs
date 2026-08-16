using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

// ═══════════════════════════════════════════════════════════════
//  IConnectivityService
//  Monitors internet connectivity and sync status.
//  The UI observes Status to show the connection indicator.
// ═══════════════════════════════════════════════════════════════

public interface IConnectivityService
{
    ConnectivityStatus Status { get; }
    bool IsOnline { get; }
    int PendingCount { get; }

    event EventHandler<ConnectivityStatus> StatusChanged;

    void UpdateStatus(ConnectivityStatus status);
    void IncrementPending();
    void DecrementPending(int count = 1);
}
