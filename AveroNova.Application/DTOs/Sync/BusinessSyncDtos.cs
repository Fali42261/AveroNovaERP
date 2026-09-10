using AveroNova.Domain.Enums;

namespace AveroNova.Application.DTOs.Sync;

public sealed class BusinessSyncBatchRequest
{
    public List<BusinessSyncItemRequest> Items { get; set; } = [];
}

public sealed class BusinessSyncItemRequest
{
    public Guid QueueId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid CompanyId { get; set; }
    public SyncOperation Operation { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime ClientUpdatedAtUtc { get; set; }
}

public sealed class BusinessSyncBatchResponse
{
    public DateTime ServerTimeUtc { get; set; }
    public List<BusinessSyncItemResult> Items { get; set; } = [];
}

public sealed class BusinessSyncItemResult
{
    public Guid QueueId { get; set; }
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime ServerUpdatedAtUtc { get; set; }
}
