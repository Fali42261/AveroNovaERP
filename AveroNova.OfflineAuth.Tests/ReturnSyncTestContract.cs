namespace AveroNova.App.UI.Services;

// LocalReturnService is source-linked into this test assembly. The production
// IReturnSyncService lives beside the MAUI sync implementation, so this tiny
// contract keeps the persistence tests platform-neutral while matching the
// production constructor dependency exactly.
public interface IReturnSyncService
{
    Task<int> SyncPendingAsync(CancellationToken cancellationToken = default);
}
