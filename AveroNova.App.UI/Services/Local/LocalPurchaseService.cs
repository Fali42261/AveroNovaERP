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
        if (companyId == Guid.Empty) return [];
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Purchases.AsNoTracking().Include(x => x.Items).Where(x => x.CompanyId == companyId && !x.IsDeleted).OrderByDescending(x => x.PurchaseDate).ThenByDescending(x => x.CreatedAt).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<PurchaseModel?> GetByIdAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync(); var row = await db.Purchases.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        return row == null ? null : Map(row);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(PurchaseModel purchase)
    {
        var validation = Validate(purchase); if (validation != null) return (false, validation);
        await using var db = await _factory.CreateDbContextAsync(); await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow; var qty = Quantities(purchase.Items); var products = await LoadProductsAsync(db, purchase.CompanyId, qty.Keys);
            if (products.Count != qty.Count) return (false, "One or more products were not found.");
            purchase.LocalId = purchase.LocalId == Guid.Empty ? Guid.NewGuid() : purchase.LocalId; purchase.SyncStatus = SyncStatus.PendingSync;
            db.Purchases.Add(ToEntity(purchase, now));
            if (AffectsStock(purchase.Status)) foreach (var product in products) ApplyStockChange(db, purchase.CompanyId, product, qty[product.Id], purchase.PurchaseNumber, "Purchase received", now);
            await db.SaveChangesAsync(); await tx.CommitAsync(); return (true, null);
        }
        catch (Exception ex) { await tx.RollbackAsync(); System.Diagnostics.Debug.WriteLine($"[AveroNova] Offline purchase create failed: {ex}"); return (false, "Unable to save purchase locally."); }
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(PurchaseModel purchase)
    {
        var validation = Validate(purchase, true); if (validation != null) return (false, validation);
        await using var db = await _factory.CreateDbContextAsync(); var entity = await db.Purchases.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == purchase.LocalId && !x.IsDeleted);
        if (entity == null) return (false, "Purchase not found."); await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            var oldQty = AffectsStock((PurchaseStatus)entity.Status) ? entity.Items.Where(x => !x.IsDeleted).GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity)) : new Dictionary<Guid, int>();
            var newQty = AffectsStock(purchase.Status) ? Quantities(purchase.Items) : new Dictionary<Guid, int>();
            var ids = oldQty.Keys.Union(newQty.Keys).ToList(); var products = await LoadProductsAsync(db, entity.CompanyId, ids);
            if (products.Count != ids.Count) return (false, "One or more products were not found.");
            foreach (var product in products)
            {
                var stockDelta = newQty.GetValueOrDefault(product.Id) - oldQty.GetValueOrDefault(product.Id);
                if (stockDelta < 0 && product.Stock < -stockDelta) return (false, $"Cannot reduce received quantity for {product.Name}; current stock is too low.");
            }
            foreach (var product in products)
            {
                var stockDelta = newQty.GetValueOrDefault(product.Id) - oldQty.GetValueOrDefault(product.Id); if (stockDelta == 0) continue;
                ApplyStockChange(db, entity.CompanyId, product, stockDelta, purchase.PurchaseNumber, stockDelta > 0 ? "Purchase received / edited" : "Purchase receipt reversed", now);
            }
            entity.SupplierId = purchase.SupplierId; entity.SupplierName = purchase.SupplierName; entity.PurchaseDate = purchase.PurchaseDate; entity.DueDate = purchase.DueDate;
            entity.PaymentMethod = (int)purchase.PaymentMethod; entity.PaidAmount = purchase.PaidAmount; entity.Reference = purchase.Reference; entity.Notes = purchase.Notes; entity.Status = (int)purchase.Status;
            entity.UpdatedAt = now; entity.SyncStatus = (int)SyncStatus.PendingSync; foreach (var old in entity.Items) old.IsDeleted = true;
            entity.Items = purchase.Items.Select(x => new PurchaseItem { Id = Guid.NewGuid(), PurchaseId = entity.Id, ProductId = x.ProductId, ProductName = x.ProductName, SKU = x.SKU, UnitPrice = x.UnitPrice, Quantity = x.Quantity, TaxPct = x.TaxPct, CreatedAt = now, UpdatedAt = now, IsDeleted = false }).ToList();
            await db.SaveChangesAsync(); await tx.CommitAsync(); return (true, null);
        }
        catch (Exception ex) { await tx.RollbackAsync(); System.Diagnostics.Debug.WriteLine($"[AveroNova] Offline purchase update failed: {ex}"); return (false, "Unable to update purchase locally."); }
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync(); var entity = await db.Purchases.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (entity == null) return (false, "Purchase not found."); await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            if (AffectsStock((PurchaseStatus)entity.Status))
            {
                var qty = entity.Items.Where(x => !x.IsDeleted).GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity)); var products = await LoadProductsAsync(db, entity.CompanyId, qty.Keys);
                foreach (var product in products) if (product.Stock < qty[product.Id]) return (false, $"Cannot delete this received purchase because {product.Name} stock has already been used.");
                foreach (var product in products) ApplyStockChange(db, entity.CompanyId, product, -qty[product.Id], entity.PurchaseNumber, "Purchase deleted / receipt reversed", now);
            }
            entity.IsDeleted = true; entity.UpdatedAt = now; entity.SyncStatus = (int)SyncStatus.PendingSync; await db.SaveChangesAsync(); await tx.CommitAsync(); return (true, null);
        }
        catch (Exception ex) { await tx.RollbackAsync(); System.Diagnostics.Debug.WriteLine($"[AveroNova] Offline purchase delete failed: {ex}"); return (false, "Unable to delete purchase locally."); }
    }

    public async Task<string> GetNextPurchaseNumberAsync(Guid companyId)
    {
        await using var db = await _factory.CreateDbContextAsync(); var count = await db.Purchases.CountAsync(x => x.CompanyId == companyId && !x.IsDeleted); return $"PO-{DateTime.Today:yyyy}-{count + 1:D4}";
    }

    private static string? Validate(PurchaseModel p, bool requireId = false)
    {
        if (requireId && p.LocalId == Guid.Empty) return "Purchase is required."; if (p.CompanyId == Guid.Empty) return "Company is required.";
        if (string.IsNullOrWhiteSpace(p.SupplierName)) return "Supplier is required."; if (p.Items.Count == 0) return "Add at least one line item.";
        if (p.Items.Any(x => x.ProductId == Guid.Empty || x.Quantity <= 0 || x.UnitPrice < 0)) return "Purchase contains an invalid line item.";
        if (p.PaidAmount < 0 || p.PaidAmount > p.GrandTotal) return "Paid amount is invalid."; return null;
    }

    private static bool AffectsStock(PurchaseStatus status) => status == PurchaseStatus.Received;
    private static Dictionary<Guid, int> Quantities(IEnumerable<PurchaseLineItem> items) => items.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
    private static async Task<List<Product>> LoadProductsAsync(AppDbContext db, Guid companyId, IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList(); if (list.Count == 0) return [];
        return await db.Products.Where(x => x.CompanyId == companyId && !x.IsDeleted && list.Contains(x.Id)).ToListAsync();
    }
    private static void ApplyStockChange(AppDbContext db, Guid companyId, Product product, int signedChange, string reference, string notes, DateTime now)
    {
        var before = product.Stock; product.Stock += signedChange; product.UpdatedAt = now;
        db.StockMovements.Add(new StockMovement { Id = Guid.NewGuid(), CompanyId = companyId, ProductId = product.Id, MovementType = (int)(signedChange >= 0 ? StockMovementType.In : StockMovementType.Out), Quantity = signedChange, StockBefore = before, StockAfter = product.Stock, Reference = reference, Notes = notes, CreatedBy = "Offline ERP", SyncStatus = (int)SyncStatus.PendingSync, CreatedAt = now, UpdatedAt = now, IsDeleted = false });
    }

    private static Purchase ToEntity(PurchaseModel model, DateTime now) => new()
    {
        Id = model.LocalId, CompanyId = model.CompanyId, PurchaseNumber = model.PurchaseNumber, SupplierId = model.SupplierId, SupplierName = model.SupplierName, PurchaseDate = model.PurchaseDate,
        DueDate = model.DueDate, PaymentMethod = (int)model.PaymentMethod, PaidAmount = model.PaidAmount, Reference = model.Reference, Notes = model.Notes, Status = (int)model.Status,
        SyncStatus = (int)SyncStatus.PendingSync, CreatedAt = now, UpdatedAt = now, IsDeleted = false,
        Items = model.Items.Select(x => new PurchaseItem { Id = Guid.NewGuid(), PurchaseId = model.LocalId, ProductId = x.ProductId, ProductName = x.ProductName, SKU = x.SKU, UnitPrice = x.UnitPrice, Quantity = x.Quantity, TaxPct = x.TaxPct, CreatedAt = now, UpdatedAt = now, IsDeleted = false }).ToList()
    };

    private static PurchaseModel Map(Purchase entity) => new()
    {
        LocalId = entity.Id, CompanyId = entity.CompanyId, PurchaseNumber = entity.PurchaseNumber, SupplierId = entity.SupplierId, SupplierName = entity.SupplierName, PurchaseDate = entity.PurchaseDate,
        DueDate = entity.DueDate, PaymentMethod = Enum.IsDefined(typeof(PaymentMethod), entity.PaymentMethod) ? (PaymentMethod)entity.PaymentMethod : PaymentMethod.Cash,
        PaidAmount = entity.PaidAmount, Reference = entity.Reference, Notes = entity.Notes, Status = Enum.IsDefined(typeof(PurchaseStatus), entity.Status) ? (PurchaseStatus)entity.Status : PurchaseStatus.Draft,
        SyncStatus = Enum.IsDefined(typeof(SyncStatus), entity.SyncStatus) ? (SyncStatus)entity.SyncStatus : SyncStatus.PendingSync, CreatedAt = entity.CreatedAt, UpdatedAt = entity.UpdatedAt ?? entity.CreatedAt, IsDeleted = entity.IsDeleted,
        Items = entity.Items.Where(x => !x.IsDeleted).Select(x => new PurchaseLineItem { ProductId = x.ProductId, ProductName = x.ProductName, SKU = x.SKU, UnitPrice = x.UnitPrice, Quantity = x.Quantity, TaxPct = x.TaxPct }).ToList()
    };
}
