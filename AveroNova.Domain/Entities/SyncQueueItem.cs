using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Entities;

/// <summary>
/// Central pending-change queue for the Sync Engine (one reusable queue — not per-module).
/// </summary>
public class SyncQueueItem : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public SyncOperation Operation { get; set; }
    public int RetryCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public RecordSyncStatus QueueStatus { get; set; } = RecordSyncStatus.Pending;
    public string? Error { get; set; }
    public string? PayloadJson { get; set; }
    public Guid? CompanyId { get; set; }
}
