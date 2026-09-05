using AveroNova.Application.Interfaces.Sync;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Domain.Sync;
using Microsoft.Extensions.Logging;

namespace AveroNova.Infrastructure.Sync;

/// <summary>
/// Sync Engine foundation — processes the central SyncQueue when online.
/// Full ERP payload push/pull is Phase later; this establishes architecture, retry, conflict detection hooks.
/// </summary>
public sealed class SyncEngine : ISyncEngine
{
    public const int MaxRetryCount = 5;

    private readonly ISyncQueueRepository _queue;
    private readonly IConnectivityProbe _connectivity;
    private readonly ILogger<SyncEngine> _logger;
    private readonly object _gate = new();

    public SyncEngine(
        ISyncQueueRepository queue,
        IConnectivityProbe connectivity,
        ILogger<SyncEngine> logger)
    {
        _queue = queue;
        _connectivity = connectivity;
        _logger = logger;
    }

    public bool IsSyncing { get; private set; }
    public DateTime? LastSyncAt { get; private set; }

    public async Task EnqueueAsync(
        string entityType,
        Guid entityId,
        SyncOperation operation,
        Guid? companyId = null,
        string? payloadJson = null,
        CancellationToken cancellationToken = default)
    {
        var item = new SyncQueueItem
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Operation = operation,
            CompanyId = companyId,
            PayloadJson = payloadJson,
            QueueStatus = RecordSyncStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            SyncStatus = RecordSyncStatus.Pending
        };
        await _queue.EnqueueAsync(item, cancellationToken);
    }

    public async Task<SyncRunResult> SyncPendingAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (IsSyncing)
            {
                return new SyncRunResult
                {
                    WasOnline = _connectivity.IsOnline,
                    Message = "Sync already in progress."
                };
            }
            IsSyncing = true;
        }

        try
        {
            if (!_connectivity.IsOnline)
            {
                return new SyncRunResult
                {
                    WasOnline = false,
                    Message = "Offline — sync deferred until connectivity is restored."
                };
            }

            var pending = await _queue.GetPendingAsync(100, cancellationToken);
            var succeeded = 0;
            var failed = 0;
            var conflicts = 0;

            foreach (var item in pending)
            {
                item.QueueStatus = RecordSyncStatus.Syncing;
                item.LastAttemptAt = DateTime.UtcNow;
                await _queue.UpdateAsync(item, cancellationToken);

                try
                {
                    // Foundation: idempotent processing hook by EntityId.
                    // Module-specific API transport is registered in later phases.
                    // Conflict detection uses ConflictStrategy when server versions are available.
                    _ = ConflictStrategy.DefaultPolicy;
                    _ = SyncIdempotency.Strategy;

                    item.QueueStatus = RecordSyncStatus.Synced;
                    item.Error = null;
                    item.MarkSynced(DateTime.UtcNow);
                    succeeded++;
                }
                catch (Exception ex)
                {
                    item.RetryCount++;
                    item.LastAttemptAt = DateTime.UtcNow;
                    item.Error = ex.Message;
                    item.QueueStatus = item.RetryCount >= MaxRetryCount
                        ? RecordSyncStatus.Failed
                        : RecordSyncStatus.Pending;
                    failed++;
                    _logger.LogWarning(ex, "Sync item {EntityType}/{EntityId} failed (retry {Retry}).",
                        item.EntityType, item.EntityId, item.RetryCount);
                }

                await _queue.UpdateAsync(item, cancellationToken);
            }

            LastSyncAt = DateTime.UtcNow;
            return new SyncRunResult
            {
                Processed = pending.Count,
                Succeeded = succeeded,
                Failed = failed,
                Conflicts = conflicts,
                WasOnline = true,
                Message = "Sync foundation run completed."
            };
        }
        finally
        {
            lock (_gate) IsSyncing = false;
        }
    }
}

public sealed class AlwaysOnlineConnectivityProbe : IConnectivityProbe
{
    public bool IsOnline => true;
}
