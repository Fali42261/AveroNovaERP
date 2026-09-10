namespace AveroNova.App.UI.Models;

public class SyncHistoryModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; }
    public int ItemsSynced { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
}

public sealed class SyncConflictModel
{
    public Guid QueueId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? LocalPayloadJson { get; set; }
    public string? ServerPayloadJson { get; set; }
    public long ServerVersion { get; set; }
    public string Error { get; set; } = string.Empty;
    public DateTime DetectedAtUtc { get; set; }
}
