using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.Domain.Enums;

namespace AveroNova.App.UI.Services;

internal static class LocalSyncQueueWriter
{
    public static void Enqueue(
        LocalAppDbContext db,
        string entityType,
        Guid entityId,
        Guid companyId,
        SyncOperation operation,
        object? payload,
        DateTime utcNow)
    {
        db.SyncQueue.Add(new LocalSyncQueueEntity
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Operation = (int)operation,
            Status = (int)RecordSyncStatus.Pending,
            RetryCount = 0,
            CreatedAt = utcNow,
            CompanyId = companyId,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload)
        });
    }
}
