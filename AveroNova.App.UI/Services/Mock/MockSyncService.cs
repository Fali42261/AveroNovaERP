using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

// ═══════════════════════════════════════════════════════════════
//  MockSyncService
//
//  OFFLINE FLOW:
//    ViewModel → Service → Local Database
//                        → Pending Sync Queue
//                        → SyncService → API → Server Database
//
//  TODO: Implement real SyncService with conflict resolution,
//        retry logic, and error recovery during backend phase.
// ═══════════════════════════════════════════════════════════════

public class MockSyncService : ISyncService
{
    private bool      _isSyncing;
    private DateTime? _lastSyncAt  = DateTime.UtcNow.AddHours(-1);
    private int       _pending     = 3;
    private int       _failed      = 1;

    public bool      IsSyncing    => _isSyncing;
    public DateTime? LastSyncAt   => _lastSyncAt;
    public int       PendingCount => _pending;
    public int       FailedCount  => _failed;

    public event EventHandler<SyncHistoryModel>? SyncCompleted;

    public async Task<bool> SyncNowAsync()
    {
        _isSyncing = true;
        await Task.Delay(2000); // Simulate network call
        _isSyncing  = false;
        _lastSyncAt = DateTime.UtcNow;
        _pending    = 0;
        _failed     = 0;

        var history = new SyncHistoryModel
        {
            SyncedAt    = DateTime.UtcNow,
            Success     = true,
            ItemsSynced = 3,
            Module      = "All Modules",
            Message     = "Sync completed successfully."
        };
        MockDataStore.SyncHistory.Insert(0, history);
        SyncCompleted?.Invoke(this, history);
        return true;
    }

    public async Task<bool> RetryFailedAsync()
    {
        _isSyncing = true;
        await Task.Delay(1500);
        _isSyncing = false;
        _failed    = 0;
        return true;
    }

    public Task<List<SyncHistoryModel>> GetHistoryAsync()
        => Task.FromResult(MockDataStore.SyncHistory.ToList());
}
