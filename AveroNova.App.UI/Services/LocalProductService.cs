using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public sealed class LocalProductService : IProductService
{
    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly IAppSessionContext _session;

    public LocalProductService(IDbContextFactory<LocalAppDbContext> dbFactory, IAppSessionContext session)
    {
        _dbFactory = dbFactory;
        _session = session;
    }

    public async Task<List<ProductModel>> GetAllAsync(Guid companyId)
    {
        if (!Allows(companyId))
            return [];

        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.Products.AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .OrderBy(p => p.Name)
            .ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<ProductModel?> GetByIdAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return row is null || !Allows(row.CompanyId) ? null : Map(row);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(ProductModel product)
    {
        if (!Allows(product.CompanyId))
            return (false, "You do not have access to this company.");
        if (string.IsNullOrWhiteSpace(product.Name))
            return (false, "Product name is required.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        product.LocalId = product.LocalId == Guid.Empty ? Guid.NewGuid() : product.LocalId;
        var row = ToEntity(product, now);
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        db.Products.Add(row);
        LocalSyncQueueWriter.Enqueue(db, "Product", row.Id, row.CompanyId, SyncOperation.Create, Payload(row), now);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(ProductModel product)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Products.FirstOrDefaultAsync(p => p.Id == product.LocalId);
        if (row is null || !Allows(row.CompanyId))
            return (false, "Product not found.");

        var now = DateTime.UtcNow;
        Apply(row, product, now);
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        LocalSyncQueueWriter.Enqueue(db, "Product", row.Id, row.CompanyId, SyncOperation.Update, Payload(row), now);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (row is null || !Allows(row.CompanyId))
            return (false, "Product not found.");

        db.Products.Remove(row);
        LocalSyncQueueWriter.Enqueue(db, "Product", row.Id, row.CompanyId, SyncOperation.Delete, new { row.Id }, DateTime.UtcNow);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<ProductModel>> SearchAsync(Guid companyId, string query)
    {
        var all = await GetAllAsync(companyId);
        if (string.IsNullOrWhiteSpace(query))
            return all;
        return all.Where(p =>
                p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || p.SKU.Contains(query, StringComparison.OrdinalIgnoreCase)
                || p.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<List<ProductModel>> GetLowStockAsync(Guid companyId)
        => (await GetAllAsync(companyId)).Where(p => p.IsLowStock).ToList();

    private bool Allows(Guid companyId)
        => _session.CurrentCompanyId is Guid current && current != Guid.Empty && current == companyId;

    private static ProductModel Map(LocalProductEntity row)
        => new()
        {
            LocalId = row.Id,
            ServerId = row.ServerId?.ToString("D"),
            CompanyId = row.CompanyId,
            Name = row.Name,
            SKU = row.SKU,
            Barcode = row.Barcode,
            Category = row.Category,
            Brand = row.Brand,
            Unit = row.Unit,
            PurchasePrice = row.PurchasePrice,
            SellingPrice = row.SellingPrice,
            TaxPercent = row.TaxPercent,
            Stock = row.Stock,
            MinimumStock = row.MinimumStock,
            Description = row.Description,
            Status = (ProductStatus)row.Status,
            CreatedAt = row.CreatedAtUtc,
            UpdatedAt = row.UpdatedAtUtc,
            LastSyncedAt = row.LastSyncedAtUtc,
            SyncStatus = ToUiStatus(row.SyncStatus)
        };

    private static LocalProductEntity ToEntity(ProductModel model, DateTime now)
        => new()
        {
            Id = model.LocalId,
            CompanyId = model.CompanyId,
            Name = model.Name.Trim(),
            SKU = model.SKU.Trim(),
            Barcode = model.Barcode,
            Category = model.Category,
            Brand = model.Brand,
            Unit = string.IsNullOrWhiteSpace(model.Unit) ? "pcs" : model.Unit,
            PurchasePrice = model.PurchasePrice,
            SellingPrice = model.SellingPrice,
            TaxPercent = model.TaxPercent,
            Stock = model.Stock,
            MinimumStock = model.MinimumStock,
            Description = model.Description,
            Status = (int)model.Status,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    private static void Apply(LocalProductEntity row, ProductModel model, DateTime now)
    {
        row.Name = model.Name.Trim();
        row.SKU = model.SKU.Trim();
        row.Barcode = model.Barcode;
        row.Category = model.Category;
        row.Brand = model.Brand;
        row.Unit = model.Unit;
        row.PurchasePrice = model.PurchasePrice;
        row.SellingPrice = model.SellingPrice;
        row.TaxPercent = model.TaxPercent;
        row.Stock = model.Stock;
        row.MinimumStock = model.MinimumStock;
        row.Description = model.Description;
        row.Status = (int)model.Status;
        row.UpdatedAtUtc = now;
        row.SyncError = null;
    }

    private static object Payload(LocalProductEntity row)
        => new { row.Id, row.CompanyId, row.Name, row.SKU, row.SellingPrice, row.Stock };

    private static SyncStatus ToUiStatus(int status) => (RecordSyncStatus)status switch
    {
        RecordSyncStatus.Synced => SyncStatus.Synced,
        RecordSyncStatus.Failed => SyncStatus.SyncFailed,
        _ => SyncStatus.PendingSync
    };
}
