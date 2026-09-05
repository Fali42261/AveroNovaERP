using AveroNova.Application.Interfaces.Sync;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Sync;

public sealed class SyncQueueRepository : ISyncQueueRepository
{
    private readonly AppDbContext _db;

    public SyncQueueRepository(AppDbContext db) => _db = db;

    public async Task EnqueueAsync(SyncQueueItem item, CancellationToken cancellationToken = default)
    {
        await _db.SyncQueueItems.AddAsync(item, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SyncQueueItem>> GetPendingAsync(int take, CancellationToken cancellationToken = default)
    {
        return await _db.SyncQueueItems
            .Where(x => !x.IsDeleted &&
                        (x.QueueStatus == RecordSyncStatus.Pending || x.QueueStatus == RecordSyncStatus.Failed))
            .OrderBy(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(SyncQueueItem item, CancellationToken cancellationToken = default)
    {
        _db.SyncQueueItems.Update(item);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
