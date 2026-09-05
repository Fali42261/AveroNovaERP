using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public sealed class LocalInventoryService : IInventoryService
{
    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly IAppSessionContext _session;
    private readonly ISyncService _sync;
    private readonly IConnectivityService _connectivity;

    public LocalInventoryService(
        IDbContextFactory<LocalAppDbContext> dbFactory,
        IAppSessionContext session,
        ISyncService sync,
        IConnectivityService connectivity)
    {
        _dbFactory = dbFactory;
        _session = session;
        _sync = sync;
        _connectivity = connectivity;
    }

    public async Task<List<InventoryItemModel>> GetInventoryAsync(Guid companyId)
    {
        if (!Allows(companyId)) return [];

        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Products.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Name)
            .Select(x => new InventoryItemModel
            {
                LocalId = x.Id,
                ProductId = x.Id,
                ProductName = x.Name,
                SKU = x.SKU,
                Category = x.Category,
                CurrentStock = x.Stock,
                AvailableStock = x.Stock,
                ReservedStock = 0,
                MinimumStock = x.MinimumStock,
                LastUpdated = x.UpdatedAtUtc,
                CompanyId = x.CompanyId,
                CreatedAt = x.CreatedAtUtc,
                UpdatedAt = x.UpdatedAtUtc,
                SyncStatus = ToUiStatus(x.SyncStatus)
            })
            .ToListAsync();
    }

    public async Task<InventoryItemModel?> GetByProductIdAsync(Guid productId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId);
        if (product is null || !Allows(product.CompanyId)) return null;
        return (await GetInventoryAsync(product.CompanyId)).FirstOrDefault(x => x.ProductId == productId);
    }

    public async Task<List<StockMovementModel>> GetMovementsAsync(Guid companyId, Guid? productId = null)
    {
        if (!Allows(companyId)) return [];

        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.StockMovements.AsNoTracking().Where(x => x.CompanyId == companyId);
        if (productId.HasValue)
            query = query.Where(x => x.ProductId == productId.Value);

        return await query.OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new StockMovementModel
            {
                LocalId = x.Id,
                ServerId = x.ServerId.HasValue ? x.ServerId.Value.ToString("D") : null,
                ProductId = x.ProductId,
                ProductName = x.ProductName,
                SKU = x.SKU,
                Type = (StockMovementType)x.Type,
                Quantity = x.Quantity,
                StockBefore = x.StockBefore,
                StockAfter = x.StockAfter,
                Reference = x.Reference,
                Notes = x.Notes,
                CreatedBy = x.CreatedBy,
                CompanyId = x.CompanyId,
                CreatedAt = x.CreatedAtUtc,
                UpdatedAt = x.UpdatedAtUtc,
                LastSyncedAt = x.LastSyncedAtUtc,
                SyncStatus = ToUiStatus(x.SyncStatus)
            })
            .ToListAsync();
    }

    public async Task<(bool Ok, string? Error)> AdjustStockAsync(StockAdjustmentModel adjustment)
    {
        if (!Allows(adjustment.CompanyId))
            return (false, "You do not have access to this company.");
        if (adjustment.ProductId == Guid.Empty)
            return (false, "Select a product.");
        if (adjustment.NewStock < 0)
            return (false, "Stock cannot be negative.");
        if (string.IsNullOrWhiteSpace(adjustment.Reason))
            return (false, "Adjustment reason is required.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var product = await db.Products.FirstOrDefaultAsync(x =>
            x.Id == adjustment.ProductId && x.CompanyId == adjustment.CompanyId);
        if (product is null)
            return (false, "Inventory item not found.");

        var now = DateTime.UtcNow;
        var before = product.Stock;
        var difference = adjustment.NewStock - before;
        if (difference == 0)
            return (false, "New stock must be different from current stock.");

        product.Stock = adjustment.NewStock;
        product.UpdatedAtUtc = now;
        product.SyncStatus = (int)RecordSyncStatus.Pending;
        product.SyncError = null;

        var movement = new LocalStockMovementEntity
        {
            Id = adjustment.LocalId == Guid.Empty ? Guid.NewGuid() : adjustment.LocalId,
            CompanyId = product.CompanyId,
            ProductId = product.Id,
            ProductName = product.Name,
            SKU = product.SKU,
            Type = (int)StockMovementType.Adjustment,
            Quantity = difference,
            StockBefore = before,
            StockAfter = adjustment.NewStock,
            Reference = $"ADJ-{now:yyyyMMddHHmmss}",
            Notes = string.IsNullOrWhiteSpace(adjustment.Notes) ? adjustment.Reason.Trim() : adjustment.Notes.Trim(),
            CreatedBy = string.IsNullOrWhiteSpace(adjustment.AdjustedBy) ? "Current user" : adjustment.AdjustedBy.Trim(),
            SyncStatus = (int)RecordSyncStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.StockMovements.Add(movement);

        LocalSyncQueueWriter.Enqueue(db, "Product", product.Id, product.CompanyId, SyncOperation.Update,
            new
            {
                product.Id,
                product.CompanyId,
                product.Name,
                Sku = product.SKU,
                product.Barcode,
                product.Category,
                product.Brand,
                product.Unit,
                product.PurchasePrice,
                product.SellingPrice,
                product.TaxPercent,
                product.Stock,
                product.MinimumStock,
                product.Description,
                product.Status,
                SyncVersion = 1L
            }, now);

        LocalSyncQueueWriter.Enqueue(db, "StockMovement", movement.Id, movement.CompanyId, SyncOperation.Create,
            new
            {
                movement.Id,
                movement.CompanyId,
                movement.ProductId,
                movement.ProductName,
                Sku = movement.SKU,
                movement.Type,
                movement.Quantity,
                movement.StockBefore,
                movement.StockAfter,
                movement.Reference,
                movement.Notes,
                movement.CreatedBy,
                SyncVersion = 1L
            }, now);

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        _connectivity.IncrementPending();
        _connectivity.IncrementPending();
        if (_connectivity.IsOnline)
            _ = _sync.SyncNowAsync();

        return (true, null);
    }

    private bool Allows(Guid companyId)
        => companyId != Guid.Empty && _session.CurrentCompanyId == companyId;

    private static SyncStatus ToUiStatus(int status) => (RecordSyncStatus)status switch
    {
        RecordSyncStatus.Synced => SyncStatus.Synced,
        RecordSyncStatus.Failed => SyncStatus.SyncFailed,
        _ => SyncStatus.PendingSync
    };
}
