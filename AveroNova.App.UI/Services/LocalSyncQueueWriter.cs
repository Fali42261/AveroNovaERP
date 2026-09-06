using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.Domain.Enums;

namespace AveroNova.App.UI.Services;

internal static class LocalSyncQueueWriter
{
    public static Guid Enqueue(
        LocalAppDbContext db,
        string entityType,
        Guid entityId,
        Guid companyId,
        SyncOperation operation,
        object? payload,
        DateTime utcNow)
    {
        var id = Guid.NewGuid();
        db.SyncQueue.Add(new LocalSyncQueueEntity
        {
            Id = id,
            EntityType = entityType,
            EntityId = entityId,
            Operation = (int)operation,
            Status = (int)RecordSyncStatus.Pending,
            RetryCount = 0,
            CreatedAt = utcNow,
            CompanyId = companyId,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload)
        });
        return id;
    }
}
