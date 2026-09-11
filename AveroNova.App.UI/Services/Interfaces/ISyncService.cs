using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

// ═══════════════════════════════════════════════════════════════
//  ISyncService
//
//  OFFLINE FLOW:
//    ViewModel → Service → Local Database
//                        → Pending Sync Queue
//                        → SyncService → API → Server Database
//
//  The SyncService runs in the background and processes the
//  pending queue when internet connectivity is restored.
//
//  Implementations must preserve per-record conflicts and support retry/error recovery.
// ═══════════════════════════════════════════════════════════════

public interface ISyncService
{
    bool              IsSyncing      { get; }
    DateTime?         LastSyncAt     { get; }
    int               PendingCount   { get; }
    int               FailedCount    { get; }

    event EventHandler<SyncHistoryModel> SyncCompleted;

    Task<bool> SyncNowAsync();
    Task<bool> RetryFailedAsync();
    Task<List<SyncHistoryModel>> GetHistoryAsync();
    Task<List<SyncConflictModel>> GetConflictsAsync();
    Task<bool> RetryConflictUsingLocalAsync(Guid queueId);

}
