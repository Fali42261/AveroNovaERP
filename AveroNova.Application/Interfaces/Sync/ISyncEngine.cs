using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;

namespace AveroNova.Application.Interfaces.Sync;

public interface ISyncQueueRepository
{
    Task EnqueueAsync(SyncQueueItem item, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncQueueItem>> GetPendingAsync(int take, CancellationToken cancellationToken = default);
    Task UpdateAsync(SyncQueueItem item, CancellationToken cancellationToken = default);
}

public interface ISyncEngine
{
    bool IsSyncing { get; }
    DateTime? LastSyncAt { get; }
    Task<SyncRunResult> SyncPendingAsync(CancellationToken cancellationToken = default);
    Task EnqueueAsync(string entityType, Guid entityId, SyncOperation operation, Guid? companyId = null, string? payloadJson = null, CancellationToken cancellationToken = default);
}

public sealed class SyncRunResult
{
    public int Processed { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public int Conflicts { get; init; }
    public bool WasOnline { get; init; }
    public string? Message { get; init; }
}

public interface IConnectivityProbe
{
    bool IsOnline { get; }
}
