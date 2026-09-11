using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;

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
        var expectedServerVersion = db.SyncQueue.AsNoTracking()
            .Where(x => x.EntityType == entityType
                        && x.EntityId == entityId
                        && x.Status == (int)RecordSyncStatus.Synced
                        && x.ServerVersion > 0)
            .OrderByDescending(x => x.SyncedAt)
            .Select(x => x.ServerVersion)
            .FirstOrDefault();

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
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload),
            ExpectedServerVersion = expectedServerVersion
        });
    }
}
