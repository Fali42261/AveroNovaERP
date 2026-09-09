using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public sealed class LocalPurchaseService
    : IPurchaseService
{
    private readonly IDbContextFactory<LocalAppDbContext> _factory;
    private readonly IAppSessionContext _session;

    public LocalPurchaseService(IDbContextFactory<LocalAppDbContext> factory, IAppSessionContext session)
    {
        _factory = factory;
        _session = session;
    }

    public async Task<List<PurchaseModel>> GetAllAsync(Guid companyId)
    {
        if (!Allows(companyId)) return [];
        await using var db = await _factory.CreateDbContextAsync();
        return (await db.Purchases.AsNoTracking().Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.PurchaseDate).ToListAsync()).Select(Map).ToList();
    }

    public async Task<PurchaseModel?> GetByIdAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Purchases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return row is null || !Allows(row.CompanyId) ? null : Map(row);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(PurchaseModel model)
    {
        var error = await ValidateAsync(model);
        if (error is not null) return (false, error);
        await using var db = await _factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (await db.Purchases.AnyAsync(x => x.CompanyId == model.CompanyId && x.PurchaseNumber == model.PurchaseNumber.Trim()))
            return (false, "Purchase number already exists.");

        var now = DateTime.UtcNow;
        model.LocalId = model.LocalId == Guid.Empty ? Guid.NewGuid() : model.LocalId;
        model.PaidAmount = 0;
        if (model.Status == PurchaseStatus.Received)
        {
            error = await ApplyStockChangesAsync(db, model.CompanyId, model.LocalId, model.PurchaseNumber, [], model.Items, now);
            if (error is not null) return (false, error);
        }
        var row = ToEntity(model, now);
        db.Purchases.Add(row);
        LocalSyncQueueWriter.Enqueue(db, "Purchase", row.Id, row.CompanyId, SyncOperation.Create, Payload(row), now);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(PurchaseModel model)
    {
        var error = await ValidateAsync(model);
        if (error is not null) return (false, error);
        await using var db = await _factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var row = await db.Purchases.FirstOrDefaultAsync(x => x.Id == model.LocalId);
        if (row is null || !Allows(row.CompanyId) || row.CompanyId != model.CompanyId) return (false, "Purchase not found.");
        if (await db.Purchases.AnyAsync(x => x.CompanyId == model.CompanyId && x.Id != model.LocalId && x.PurchaseNumber == model.PurchaseNumber.Trim()))
            return (false, "Purchase number already exists.");

        var linked = db.Payments.Where(p => p.CompanyId == row.CompanyId && p.InvoiceId == row.Id && p.IsSupplier);
        if (await db.PurchaseReturns.AnyAsync(r => r.CompanyId == row.CompanyId && r.PurchaseId == row.Id))
            return (false, "Delete purchase returns before editing or cancelling this purchase.");
        var paid = await linked.Where(p => p.Status == (int)PaymentStatus.Completed).SumAsync(p => p.Amount);
        if (paid > model.GrandTotal) return (false, "Purchase total cannot be less than supplier payments already applied.");
        if (model.Status == PurchaseStatus.Cancelled && await linked.AnyAsync())
            return (false, "Delete linked supplier payments before cancelling this purchase.");

        var oldItems = Deserialize(row.ItemsJson);
        var oldReceived = row.Status == (int)PurchaseStatus.Received ? oldItems : [];
        var newReceived = model.Status == PurchaseStatus.Received ? model.Items : [];
        var now = DateTime.UtcNow;
        error = await ApplyStockChangesAsync(db, row.CompanyId, row.Id, row.PurchaseNumber, oldReceived, newReceived, now);
        if (error is not null) return (false, error);

        Apply(row, model, now);
        row.PaidAmount = paid;
        LocalSyncQueueWriter.Enqueue(db, "Purchase", row.Id, row.CompanyId, SyncOperation.Update, Payload(row), now);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var row = await db.Purchases.FirstOrDefaultAsync(x => x.Id == id);
        if (row is null || !Allows(row.CompanyId)) return (false, "Purchase not found.");
        if (await db.Payments.AnyAsync(p => p.CompanyId == row.CompanyId && p.InvoiceId == row.Id && p.IsSupplier))
            return (false, "Delete linked supplier payments before deleting this purchase.");
        if (await db.PurchaseReturns.AnyAsync(r => r.CompanyId == row.CompanyId && r.PurchaseId == row.Id))
            return (false, "Delete purchase returns before deleting this purchase.");
        if (row.Status == (int)PurchaseStatus.Received)
        {
            var error = await ApplyStockChangesAsync(db, row.CompanyId, row.Id, row.PurchaseNumber, Deserialize(row.ItemsJson), [], DateTime.UtcNow);
            if (error is not null) return (false, error);
        }
        db.Purchases.Remove(row);
        LocalSyncQueueWriter.Enqueue(db, "Purchase", row.Id, row.CompanyId, SyncOperation.Delete, new { row.Id, row.CompanyId }, DateTime.UtcNow);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (true, null);
    }

    public async Task<string> GetNextPurchaseNumberAsync(Guid companyId)
    {
        if (!Allows(companyId)) return string.Empty;
        await using var db = await _factory.CreateDbContextAsync();
        var prefix = $"PO-{DateTime.Today:yyyy}-";
        var numbers = await db.Purchases.AsNoTracking().Where(x => x.CompanyId == companyId && x.PurchaseNumber.StartsWith(prefix)).Select(x => x.PurchaseNumber).ToListAsync();
        var max = numbers.Select(x => int.TryParse(x[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty().Max();
        return $"{prefix}{max + 1:D4}";
    }

    private async Task<string?> ValidateAsync(PurchaseModel model)
    {
        if (!Allows(model.CompanyId)) return "You do not have access to this company.";
        if (model.SupplierId == Guid.Empty) return "Select a supplier.";
        if (string.IsNullOrWhiteSpace(model.PurchaseNumber)) return "Purchase number is required.";
        if (model.DueDate.Date < model.PurchaseDate.Date) return "Due date cannot be before purchase date.";
        if (model.Items.Count == 0) return "Add at least one purchase item.";
        if (!Enum.IsDefined(model.Status) || !Enum.IsDefined(model.PaymentMethod)) return "Purchase status or payment method is invalid.";
        if (model.Items.Any(x => x.ProductId == Guid.Empty || x.Quantity <= 0 || x.UnitPrice < 0 || x.TaxPct is < 0 or > 100)) return "Purchase item values are invalid.";
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Suppliers.AnyAsync(x => x.Id == model.SupplierId && x.CompanyId == model.CompanyId && x.IsActive) ? null : "Supplier not found for this company.";
    }

    private static async Task<string?> ApplyStockChangesAsync(LocalAppDbContext db, Guid companyId, Guid purchaseId,
        string purchaseNumber, List<PurchaseLineItem> oldItems, List<PurchaseLineItem> newItems, DateTime now)
    {
        var oldQty = oldItems.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var newQty = newItems.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var productIds = oldQty.Keys.Union(newQty.Keys).ToList();
        if (productIds.Count == 0) return null;
        var products = await db.Products.Where(x => x.CompanyId == companyId && productIds.Contains(x.Id)).ToListAsync();
        if (products.Count != productIds.Count) return "One or more purchase products were not found for this company.";

        foreach (var product in products)
        {
            var delta = newQty.GetValueOrDefault(product.Id) - oldQty.GetValueOrDefault(product.Id);
            if (delta == 0) continue;
            if (product.Stock + delta < 0) return $"Cannot reverse {purchaseNumber}; {product.Name} stock would become negative.";
            var before = product.Stock;
            product.Stock += delta;
            product.SyncStatus = (int)RecordSyncStatus.Pending;
            product.SyncError = null;
            product.UpdatedAtUtc = now;
            var movement = new LocalStockMovementEntity
            {
                Id = Guid.NewGuid(), CompanyId = companyId, ProductId = product.Id, ProductName = product.Name, SKU = product.SKU,
                Type = (int)(delta > 0 ? StockMovementType.In : StockMovementType.Out), Quantity = delta,
                StockBefore = before, StockAfter = product.Stock, Reference = purchaseNumber,
                Notes = $"Purchase receipt {purchaseId:D}", CreatedBy = "Purchase reconciliation",
                SyncStatus = (int)RecordSyncStatus.Pending, CreatedAtUtc = now, UpdatedAtUtc = now
            };
            db.StockMovements.Add(movement);
            LocalSyncQueueWriter.Enqueue(db, "Product", product.Id, companyId, SyncOperation.Update, new { product.Id, product.CompanyId, product.Stock, product.UpdatedAtUtc }, now);
            LocalSyncQueueWriter.Enqueue(db, "StockMovement", movement.Id, companyId, SyncOperation.Create,
                new { movement.Id, movement.CompanyId, movement.ProductId, movement.Type, movement.Quantity, movement.StockBefore, movement.StockAfter, movement.Reference, movement.Notes, movement.UpdatedAtUtc }, now);
        }
        return null;
    }

    private bool Allows(Guid id) => id != Guid.Empty && _session.CurrentCompanyId == id;
    private static List<PurchaseLineItem> Deserialize(string json) => JsonSerializer.Deserialize<List<PurchaseLineItem>>(json) ?? [];
    private static PurchaseModel Map(LocalPurchaseEntity x) => new() { LocalId=x.Id,ServerId=x.ServerId?.ToString("D"),CompanyId=x.CompanyId,PurchaseNumber=x.PurchaseNumber,SupplierId=x.SupplierId,SupplierName=x.SupplierName,PurchaseDate=x.PurchaseDate,DueDate=x.DueDate,Items=Deserialize(x.ItemsJson),PaymentMethod=(PaymentMethod)x.PaymentMethod,Reference=x.Reference,Notes=x.Notes,Status=(PurchaseStatus)x.Status,PaidAmount=x.PaidAmount,ReturnCreditAmount=x.ReturnCreditAmount,SyncStatus=ToUi(x.SyncStatus),CreatedAt=x.CreatedAtUtc,UpdatedAt=x.UpdatedAtUtc,LastSyncedAt=x.LastSyncedAtUtc };
    private static LocalPurchaseEntity ToEntity(PurchaseModel m,DateTime now)=>new(){Id=m.LocalId,CompanyId=m.CompanyId,PurchaseNumber=m.PurchaseNumber.Trim(),SupplierId=m.SupplierId,SupplierName=m.SupplierName.Trim(),PurchaseDate=m.PurchaseDate.Date,DueDate=m.DueDate.Date,ItemsJson=JsonSerializer.Serialize(m.Items),PaymentMethod=(int)m.PaymentMethod,Reference=m.Reference.Trim(),Notes=m.Notes.Trim(),Status=(int)m.Status,PaidAmount=m.PaidAmount,SyncStatus=(int)RecordSyncStatus.Pending,CreatedAtUtc=now,UpdatedAtUtc=now};
    private static void Apply(LocalPurchaseEntity x,PurchaseModel m,DateTime now){x.PurchaseNumber=m.PurchaseNumber.Trim();x.SupplierId=m.SupplierId;x.SupplierName=m.SupplierName.Trim();x.PurchaseDate=m.PurchaseDate.Date;x.DueDate=m.DueDate.Date;x.ItemsJson=JsonSerializer.Serialize(m.Items);x.PaymentMethod=(int)m.PaymentMethod;x.Reference=m.Reference.Trim();x.Notes=m.Notes.Trim();x.Status=(int)m.Status;x.SyncStatus=(int)RecordSyncStatus.Pending;x.SyncError=null;x.UpdatedAtUtc=now;}
    internal static object Payload(LocalPurchaseEntity x)=>new{x.Id,x.CompanyId,x.PurchaseNumber,x.SupplierId,x.SupplierName,x.PurchaseDate,x.DueDate,x.ItemsJson,x.PaymentMethod,x.Reference,x.Notes,x.Status,x.PaidAmount,x.ReturnCreditAmount,GrandTotal=Total(x),x.UpdatedAtUtc};
    internal static decimal Total(LocalPurchaseEntity x)=>Deserialize(x.ItemsJson).Sum(i=>i.GrandTotal);
    private static SyncStatus ToUi(int s)=>(RecordSyncStatus)s==RecordSyncStatus.Synced?SyncStatus.Synced:(RecordSyncStatus)s==RecordSyncStatus.Failed?SyncStatus.SyncFailed:SyncStatus.PendingSync;
}
