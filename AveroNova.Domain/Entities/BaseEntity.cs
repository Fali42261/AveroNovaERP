using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Entities;

/// <summary>
/// Base for all syncable Offline-First entities.
/// Id is a client-safe GUID (no server round-trip required to create offline records).
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>Monotonic per-record version used for conflict detection.</summary>
    public long SyncVersion { get; set; } = 1;

    public RecordSyncStatus SyncStatus { get; set; } = RecordSyncStatus.Pending;

    public DateTime? LastSyncedAt { get; set; }

    public void MarkPendingChange()
    {
        SyncVersion++;
        SyncStatus = RecordSyncStatus.Pending;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkSynced(DateTime utcNow)
    {
        SyncStatus = RecordSyncStatus.Synced;
        LastSyncedAt = utcNow;
        UpdatedAt = utcNow;
    }
}
