using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services.Local;

public sealed class LocalPurchaseService : IPurchaseService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public LocalPurchaseService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<PurchaseModel>> GetAllAsync(Guid companyId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Purchases.AsNoTracking().Include(x => x.Items)
            .Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .OrderByDescending(x => x.PurchaseDate).ThenByDescending(x => x.CreatedAt).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<PurchaseModel?> GetByIdAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Purchases.AsNoTracking().Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        return row == null ? null : Map(row);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(PurchaseModel purchase)
    {
        if (purchase.CompanyId == Guid.Empty) return (false, "Company is required.");
        if (purchase.SupplierId == Guid.Empty) return (false, "Supplier is required.");
        if (purchase.Items.Count == 0) return (false, "Add at least one line item.");
        if (purchase.Items.Any(x => x.ProductId == Guid.Empty || x.Quantity <= 0 || x.UnitPrice < 0)) return (false, "Purchase contains an invalid line item.");
        if (purchase.PaidAmount < 0 || purchase.PaidAmount > purchase.GrandTotal) return (false, "Paid amount is invalid.");

        await using var db = await _factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            purchase.LocalId = purchase.LocalId == Guid.Empty ? Guid.NewGuid() : purchase.LocalId;
            purchase.SyncStatus = SyncStatus.PendingSync;
            db.Purchases.Add(ToEntity(purchase, now));
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Offline purchase create failed: {ex}");
            return (false, "Unable to save purchase locally.");
        }
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(PurchaseModel purchase)
    {
        if (purchase.LocalId == Guid.Empty) return (false, "Purchase is required.");
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await db.Purchases.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == purchase.LocalId && !x.IsDeleted);
        if (entity == null) return (false, "Purchase not found.");
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            entity.SupplierId = purchase.SupplierId; entity.SupplierName = purchase.SupplierName;
            entity.PurchaseDate = purchase.PurchaseDate; entity.DueDate = purchase.DueDate;
            entity.PaymentMethod = (int)purchase.PaymentMethod; entity.PaidAmount = purchase.PaidAmount;
            entity.Reference = purchase.Reference; entity.Notes = purchase.Notes; entity.Status = (int)purchase.Status;
            entity.UpdatedAt = DateTime.UtcNow; entity.SyncStatus = (int)SyncStatus.PendingSync;
            foreach (var old in entity.Items) old.IsDeleted = true;
            entity.Items = purchase.Items.Select(x => new PurchaseItem
            {
                Id = Guid.NewGuid(), PurchaseId = entity.Id, ProductId = x.ProductId, ProductName = x.ProductName,
                SKU = x.SKU, UnitPrice = x.UnitPrice, Quantity = x.Quantity, TaxPct = x.TaxPct,
                CreatedAt = DateTime.UtcNow, IsDeleted = false
            }).ToList();
            await db.SaveChangesAsync(); await tx.CommitAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Offline purchase update failed: {ex}");
            return (false, "Unable to update purchase locally.");
        }
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await db.Purchases.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (entity == null) return (false, "Purchase not found.");
        entity.IsDeleted = true; entity.UpdatedAt = DateTime.UtcNow; entity.SyncStatus = (int)SyncStatus.PendingSync;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<string> GetNextPurchaseNumberAsync(Guid companyId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var count = await db.Purchases.CountAsync(x => x.CompanyId == companyId && !x.IsDeleted);
        return $"PO-{DateTime.Today:yyyy}-{count + 1:D4}";
    }

    private static Purchase ToEntity(PurchaseModel model, DateTime now) => new()
    {
        Id = model.LocalId, CompanyId = model.CompanyId, PurchaseNumber = model.PurchaseNumber,
        SupplierId = model.SupplierId, SupplierName = model.SupplierName, PurchaseDate = model.PurchaseDate,
        DueDate = model.DueDate, PaymentMethod = (int)model.PaymentMethod, PaidAmount = model.PaidAmount,
        Reference = model.Reference, Notes = model.Notes, Status = (int)model.Status,
        SyncStatus = (int)SyncStatus.PendingSync, CreatedAt = now, UpdatedAt = now,
        Items = model.Items.Select(x => new PurchaseItem
        {
            Id = Guid.NewGuid(), PurchaseId = model.LocalId, ProductId = x.ProductId, ProductName = x.ProductName,
            SKU = x.SKU, UnitPrice = x.UnitPrice, Quantity = x.Quantity, TaxPct = x.TaxPct,
            CreatedAt = now, UpdatedAt = now, IsDeleted = false
        }).ToList()
    };

    private static PurchaseModel Map(Purchase entity) => new()
    {
        LocalId = entity.Id, CompanyId = entity.CompanyId, PurchaseNumber = entity.PurchaseNumber,
        SupplierId = entity.SupplierId, SupplierName = entity.SupplierName, PurchaseDate = entity.PurchaseDate,
        DueDate = entity.DueDate, PaymentMethod = Enum.IsDefined(typeof(PaymentMethod), entity.PaymentMethod) ? (PaymentMethod)entity.PaymentMethod : PaymentMethod.Cash,
        PaidAmount = entity.PaidAmount, Reference = entity.Reference, Notes = entity.Notes,
        Status = Enum.IsDefined(typeof(PurchaseStatus), entity.Status) ? (PurchaseStatus)entity.Status : PurchaseStatus.Draft,
        SyncStatus = Enum.IsDefined(typeof(SyncStatus), entity.SyncStatus) ? (SyncStatus)entity.SyncStatus : SyncStatus.PendingSync,
        CreatedAt = entity.CreatedAt, UpdatedAt = entity.UpdatedAt ?? entity.CreatedAt, IsDeleted = entity.IsDeleted,
        Items = entity.Items.Where(x => !x.IsDeleted).Select(x => new PurchaseLineItem
        {
            ProductId = x.ProductId, ProductName = x.ProductName, SKU = x.SKU, UnitPrice = x.UnitPrice,
            Quantity = x.Quantity, TaxPct = x.TaxPct
        }).ToList()
    };
}
