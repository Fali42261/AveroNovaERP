using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

/// <summary>
/// Sync UI stub. Uses <see cref="IAppSessionContext"/> for User/Company/Session —
/// does not duplicate login/token logic.
/// </summary>
public class MockSyncService : ISyncService
{
    private readonly IAppSessionContext _session;
    private bool _isSyncing;
    private DateTime? _lastSyncAt = DateTime.UtcNow.AddHours(-1);
    private int _pending = 3;
    private int _failed = 1;

    public MockSyncService(IAppSessionContext session) => _session = session;

    public bool IsSyncing => _isSyncing;
    public DateTime? LastSyncAt => _lastSyncAt;
    public int PendingCount => _pending;
    public int FailedCount => _failed;

    public event EventHandler<SyncHistoryModel>? SyncCompleted;

    public async Task<bool> SyncNowAsync()
    {
        if (!_session.IsAuthenticated
            || _session.CurrentUserId is null
            || _session.CurrentCompanyId is null)
        {
            SyncCompleted?.Invoke(this, new SyncHistoryModel
            {
                SyncedAt = DateTime.UtcNow,
                Success = false,
                ItemsSynced = 0,
                Module = "Auth",
                Message = "Sync requires an authenticated user and company context."
            });
            return false;
        }

        // Auth-compatible context available for later real sync transport.
        _ = _session.ServerSessionId;
        _ = _session.Permissions;

        _isSyncing = true;
        await Task.Delay(2000);
        _isSyncing = false;
        _lastSyncAt = DateTime.UtcNow;
        _pending = 0;
        _failed = 0;

        var history = new SyncHistoryModel
        {
            SyncedAt = DateTime.UtcNow,
            Success = true,
            ItemsSynced = 3,
            Module = "All Modules",
            Message = $"Sync completed for company {_session.CurrentCompanyId}."
        };
        MockDataStore.SyncHistory.Insert(0, history);
        SyncCompleted?.Invoke(this, history);
        return true;
    }

    public async Task<bool> RetryFailedAsync()
    {
        if (!_session.IsAuthenticated)
            return false;

        _isSyncing = true;
        await Task.Delay(1500);
        _isSyncing = false;
        _failed = 0;
        return true;
    }

    public Task<List<SyncHistoryModel>> GetHistoryAsync()
        => Task.FromResult(MockDataStore.SyncHistory.ToList());
}
