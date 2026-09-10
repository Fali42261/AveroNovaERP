using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public sealed class LocalReturnService : IReturnService
{
    private readonly IDbContextFactory<LocalAppDbContext> _factory;
    private readonly IAppSessionContext _session;

    public LocalReturnService(IDbContextFactory<LocalAppDbContext> factory, IAppSessionContext session)
    {
        _factory = factory;
        _session = session;
    }

    public async Task<List<SalesReturnModel>> GetSalesReturnsAsync(Guid companyId)
    {
        if (!Allows(companyId)) return [];
        await using var db = await _factory.CreateDbContextAsync();
        return (await db.SalesReturns.AsNoTracking().Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.ReturnDate).ToListAsync()).Select(MapSales).ToList();
    }

    public async Task<SalesReturnModel?> GetSalesReturnByIdAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.SalesReturns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return row is null || !Allows(row.CompanyId) ? null : MapSales(row);
    }

    public async Task<(bool Ok, string? Error)> CreateSalesReturnAsync(SalesReturnModel model)
    {
        var error = await ValidateSalesAsync(model);
        if (error is not null) return (false, error);
        await using var db = await _factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        model.LocalId = model.LocalId == Guid.Empty ? Guid.NewGuid() : model.LocalId;
        model.ReturnNumber = await NextAsync(db, "SR", model.CompanyId, sales: true);
        var row = SalesEntity(model, now);
        db.SalesReturns.Add(row);
        LocalSyncQueueWriter.Enqueue(db, "SalesReturn", row.Id, row.CompanyId, SyncOperation.Create, SalesPayload(row), now);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateSalesReturnAsync(SalesReturnModel model)
    {
        var error = await ValidateSalesAsync(model);
        if (error is not null) return (false, error);
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.SalesReturns.FirstOrDefaultAsync(x => x.Id == model.LocalId);
        if (row is null || !Allows(row.CompanyId) || row.CompanyId != model.CompanyId) return (false, "Sales return not found.");
        Apply(row, model, DateTime.UtcNow);
        LocalSyncQueueWriter.Enqueue(db, "SalesReturn", row.Id, row.CompanyId, SyncOperation.Update, SalesPayload(row), row.UpdatedAtUtc);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteSalesReturnAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.SalesReturns.FirstOrDefaultAsync(x => x.Id == id);
        if (row is null || !Allows(row.CompanyId)) return (false, "Sales return not found.");
        db.SalesReturns.Remove(row);
        LocalSyncQueueWriter.Enqueue(db, "SalesReturn", row.Id, row.CompanyId, SyncOperation.Delete, new { row.Id, row.CompanyId }, DateTime.UtcNow);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<PurchaseReturnModel>> GetPurchaseReturnsAsync(Guid companyId)
    {
        if (!Allows(companyId)) return [];
        await using var db = await _factory.CreateDbContextAsync();
        return (await db.PurchaseReturns.AsNoTracking().Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.ReturnDate).ToListAsync()).Select(MapPurchase).ToList();
    }

    public async Task<PurchaseReturnModel?> GetPurchaseReturnByIdAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.PurchaseReturns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return row is null || !Allows(row.CompanyId) ? null : MapPurchase(row);
    }

    public async Task<(bool Ok, string? Error)> CreatePurchaseReturnAsync(PurchaseReturnModel model)
    {
        if (!Allows(model.CompanyId)) return (false, "You do not have access to this company.");
        await using var db = await _factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var purchase = await db.Purchases.FirstOrDefaultAsync(x => x.Id == model.PurchaseId && x.CompanyId == model.CompanyId);
        var error = await ValidatePurchaseAsync(db, model, purchase, null);
        if (error is not null) return (false, error);

        var now = DateTime.UtcNow;
        model.LocalId = model.LocalId == Guid.Empty ? Guid.NewGuid() : model.LocalId;
        model.ReturnNumber = await NextAsync(db, "PR", model.CompanyId, sales: false);
        error = await ReconcilePurchaseReturnAsync(db, purchase!, null, model, now);
        if (error is not null) return (false, error);

        var row = PurchaseEntity(model, now);
        db.PurchaseReturns.Add(row);
        LocalSyncQueueWriter.Enqueue(db, "PurchaseReturn", row.Id, row.CompanyId, SyncOperation.Create, PurchasePayload(row), now);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdatePurchaseReturnAsync(PurchaseReturnModel model)
    {
        if (!Allows(model.CompanyId)) return (false, "You do not have access to this company.");
        await using var db = await _factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var row = await db.PurchaseReturns.FirstOrDefaultAsync(x => x.Id == model.LocalId);
        if (row is null || row.CompanyId != model.CompanyId) return (false, "Purchase return not found.");
        var purchase = await db.Purchases.FirstOrDefaultAsync(x => x.Id == model.PurchaseId && x.CompanyId == model.CompanyId);
        var error = await ValidatePurchaseAsync(db, model, purchase, row.Id);
        if (error is not null) return (false, error);

        var now = DateTime.UtcNow;
        error = await ReconcilePurchaseReturnAsync(db, purchase!, MapPurchase(row), model, now);
        if (error is not null) return (false, error);
        Apply(row, model, now);
        LocalSyncQueueWriter.Enqueue(db, "PurchaseReturn", row.Id, row.CompanyId, SyncOperation.Update, PurchasePayload(row), row.UpdatedAtUtc);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeletePurchaseReturnAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var row = await db.PurchaseReturns.FirstOrDefaultAsync(x => x.Id == id);
        if (row is null || !Allows(row.CompanyId)) return (false, "Purchase return not found.");
        var purchase = await db.Purchases.FirstOrDefaultAsync(x => x.Id == row.PurchaseId && x.CompanyId == row.CompanyId);
        if (purchase is null) return (false, "Linked purchase not found.");

        var error = await ReconcilePurchaseReturnAsync(db, purchase, MapPurchase(row), null, DateTime.UtcNow);
        if (error is not null) return (false, error);
        db.PurchaseReturns.Remove(row);
        LocalSyncQueueWriter.Enqueue(db, "PurchaseReturn", row.Id, row.CompanyId, SyncOperation.Delete, new { row.Id, row.CompanyId }, DateTime.UtcNow);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (true, null);
    }

    private async Task<string?> ValidateSalesAsync(SalesReturnModel model)
    {
        var common = ValidateCommon(model.CompanyId, model.ReturnDate, model.Reason, model.RefundAmount, model.Status);
        if (common is not null) return common;
        if (model.InvoiceId == Guid.Empty) return "Select an invoice.";
        await using var db = await _factory.CreateDbContextAsync();
        var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.InvoiceId && x.CompanyId == model.CompanyId);
        if (invoice is null) return "Invoice not found for this company.";
        var items = JsonSerializer.Deserialize<List<InvoiceLineItem>>(invoice.ItemsJson) ?? [];
        if (model.RefundAmount > items.Sum(x => x.GrandTotal)) return "Refund cannot exceed invoice total.";
        model.InvoiceNumber = invoice.InvoiceNumber;
        model.CustomerId = invoice.CustomerId;
        model.CustomerName = invoice.CustomerName;
        return null;
    }

    private async Task<string?> ValidatePurchaseAsync(LocalAppDbContext db, PurchaseReturnModel model,
        LocalPurchaseEntity? purchase, Guid? excludingReturnId)
    {
        var common = ValidateCommon(model.CompanyId, model.ReturnDate, model.Reason, model.RefundAmount, model.Status);
        if (common is not null) return common;
        if (model.PurchaseId == Guid.Empty) return "Select a purchase.";
        if (purchase is null) return "Purchase not found for this company.";
        if (purchase.Status != (int)PurchaseStatus.Received) return "Only a received purchase can be returned.";
        if (model.ReturnDate.Date < purchase.PurchaseDate.Date) return "Return date cannot be before purchase date.";
        if (model.Items.Count == 0) return "Add at least one return item.";
        if (model.Items.Any(x => x.ProductId == Guid.Empty || x.Quantity <= 0)) return "Return item quantities must be greater than zero.";

        var purchasedItems = JsonSerializer.Deserialize<List<PurchaseLineItem>>(purchase.ItemsJson) ?? [];
        var purchasedQty = purchasedItems.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var requestedQty = model.Items.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        if (requestedQty.Keys.Any(id => !purchasedQty.ContainsKey(id))) return "A return item does not belong to the selected purchase.";

        var reservedRows = await db.PurchaseReturns.AsNoTracking()
            .Where(x => x.CompanyId == model.CompanyId && x.PurchaseId == model.PurchaseId
                        && x.Id != excludingReturnId && x.Status != (int)ReturnStatus.Rejected)
            .Select(x => x.ItemsJson).ToListAsync();
        var reservedQty = reservedRows.SelectMany(DeserializeReturnItems).GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        foreach (var (productId, quantity) in requestedQty)
            if (quantity + reservedQty.GetValueOrDefault(productId) > purchasedQty[productId])
                return "Return quantity cannot exceed the unreturned purchase quantity.";

        var maxRefund = requestedQty.Sum(pair =>
        {
            var lines = purchasedItems.Where(x => x.ProductId == pair.Key).ToList();
            var grossUnit = lines.Sum(x => x.GrandTotal) / lines.Sum(x => x.Quantity);
            return grossUnit * pair.Value;
        });
        if (model.RefundAmount > maxRefund) return "Refund cannot exceed the selected return items total.";

        model.PurchaseNumber = purchase.PurchaseNumber;
        model.SupplierId = purchase.SupplierId;
        model.SupplierName = purchase.SupplierName;
        model.Items = model.Items.GroupBy(x => x.ProductId).Select(group =>
        {
            var source = purchasedItems.First(x => x.ProductId == group.Key);
            return new ReturnLineItem { ProductId = group.Key, ProductName = source.ProductName, Quantity = group.Sum(x => x.Quantity), UnitPrice = source.UnitPrice };
        }).ToList();
        return null;
    }

    private static async Task<string?> ReconcilePurchaseReturnAsync(LocalAppDbContext db, LocalPurchaseEntity purchase,
        PurchaseReturnModel? oldReturn, PurchaseReturnModel? newReturn, DateTime now)
    {
        var oldItems = oldReturn?.Status == ReturnStatus.Completed ? oldReturn.Items : [];
        var newItems = newReturn?.Status == ReturnStatus.Completed ? newReturn.Items : [];
        var oldQty = oldItems.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var newQty = newItems.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var productIds = oldQty.Keys.Union(newQty.Keys).ToList();
        if (productIds.Count > 0)
        {
            var products = await db.Products.Where(x => x.CompanyId == purchase.CompanyId && productIds.Contains(x.Id)).ToListAsync();
            if (products.Count != productIds.Count) return "One or more return products were not found for this company.";
            foreach (var product in products)
            {
                var delta = oldQty.GetValueOrDefault(product.Id) - newQty.GetValueOrDefault(product.Id);
                if (delta == 0) continue;
                if (product.Stock + delta < 0) return $"Cannot complete return; {product.Name} stock would become negative.";
                var before = product.Stock;
                product.Stock += delta;
                product.SyncStatus = (int)RecordSyncStatus.Pending;
                product.SyncError = null;
                product.UpdatedAtUtc = now;
                var movement = new LocalStockMovementEntity
                {
                    Id = Guid.NewGuid(), CompanyId = purchase.CompanyId, ProductId = product.Id,
                    ProductName = product.Name, SKU = product.SKU, Type = (int)StockMovementType.Return,
                    Quantity = delta, StockBefore = before, StockAfter = product.Stock,
                    Reference = newReturn?.ReturnNumber ?? oldReturn?.ReturnNumber ?? purchase.PurchaseNumber,
                    Notes = $"Supplier return for purchase {purchase.Id:D}", CreatedBy = "Purchase return reconciliation",
                    SyncStatus = (int)RecordSyncStatus.Pending, CreatedAtUtc = now, UpdatedAtUtc = now
                };
                db.StockMovements.Add(movement);
                LocalSyncQueueWriter.Enqueue(db, "Product", product.Id, product.CompanyId, SyncOperation.Update,
                    new { product.Id, product.CompanyId, product.Stock, product.UpdatedAtUtc }, now);
                LocalSyncQueueWriter.Enqueue(db, "StockMovement", movement.Id, movement.CompanyId, SyncOperation.Create,
                    new { movement.Id, movement.CompanyId, movement.ProductId, movement.Type, movement.Quantity, movement.StockBefore, movement.StockAfter, movement.Reference, movement.Notes, movement.UpdatedAtUtc }, now);
            }
        }

        var excludingId = oldReturn?.LocalId ?? newReturn?.LocalId;
        var otherCredits = await db.PurchaseReturns.Where(x => x.CompanyId == purchase.CompanyId && x.PurchaseId == purchase.Id
            && x.Id != excludingId && x.Status == (int)ReturnStatus.Completed).SumAsync(x => x.RefundAmount);
        purchase.ReturnCreditAmount = otherCredits + (newReturn?.Status == ReturnStatus.Completed ? newReturn.RefundAmount : 0m);
        purchase.SyncStatus = (int)RecordSyncStatus.Pending;
        purchase.SyncError = null;
        purchase.UpdatedAtUtc = now;
        LocalSyncQueueWriter.Enqueue(db, "Purchase", purchase.Id, purchase.CompanyId, SyncOperation.Update, LocalPurchaseService.Payload(purchase), now);
        return null;
    }

    private string? ValidateCommon(Guid companyId, DateTime date, string reason, decimal refund, ReturnStatus status)
    {
        if (!Allows(companyId)) return "You do not have access to this company.";
        if (date.Date > DateTime.Today) return "Return date cannot be in the future.";
        if (string.IsNullOrWhiteSpace(reason)) return "Return reason is required.";
        if (refund <= 0) return "Refund amount must be greater than zero.";
        if (!Enum.IsDefined(status)) return "Return status is invalid.";
        return null;
    }

    private static async Task<string> NextAsync(LocalAppDbContext db, string kind, Guid companyId, bool sales)
    {
        var prefix = $"{kind}-{DateTime.Today:yyyy}-";
        var values = sales
            ? await db.SalesReturns.Where(x => x.CompanyId == companyId && x.ReturnNumber.StartsWith(prefix)).Select(x => x.ReturnNumber).ToListAsync()
            : await db.PurchaseReturns.Where(x => x.CompanyId == companyId && x.ReturnNumber.StartsWith(prefix)).Select(x => x.ReturnNumber).ToListAsync();
        var max = values.Select(x => int.TryParse(x[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty().Max();
        return $"{prefix}{max + 1:D4}";
    }

    private bool Allows(Guid companyId) => companyId != Guid.Empty && _session.CurrentCompanyId == companyId;
    private static List<ReturnLineItem> DeserializeReturnItems(string json) => JsonSerializer.Deserialize<List<ReturnLineItem>>(json) ?? [];
    private static SalesReturnModel MapSales(LocalSalesReturnEntity x) => new() { LocalId=x.Id,ServerId=x.ServerId?.ToString("D"),CompanyId=x.CompanyId,ReturnNumber=x.ReturnNumber,InvoiceId=x.InvoiceId,InvoiceNumber=x.InvoiceNumber,CustomerId=x.CustomerId,CustomerName=x.CustomerName,ReturnDate=x.ReturnDate,Items=DeserializeReturnItems(x.ItemsJson),Reason=x.Reason,Notes=x.Notes,RefundAmount=x.RefundAmount,Status=(ReturnStatus)x.Status,SyncStatus=ToUi(x.SyncStatus),CreatedAt=x.CreatedAtUtc,UpdatedAt=x.UpdatedAtUtc,LastSyncedAt=x.LastSyncedAtUtc };
    private static PurchaseReturnModel MapPurchase(LocalPurchaseReturnEntity x) => new() { LocalId=x.Id,ServerId=x.ServerId?.ToString("D"),CompanyId=x.CompanyId,ReturnNumber=x.ReturnNumber,PurchaseId=x.PurchaseId,PurchaseNumber=x.PurchaseNumber,SupplierId=x.SupplierId,SupplierName=x.SupplierName,ReturnDate=x.ReturnDate,Items=DeserializeReturnItems(x.ItemsJson),Reason=x.Reason,Notes=x.Notes,RefundAmount=x.RefundAmount,Status=(ReturnStatus)x.Status,SyncStatus=ToUi(x.SyncStatus),CreatedAt=x.CreatedAtUtc,UpdatedAt=x.UpdatedAtUtc,LastSyncedAt=x.LastSyncedAtUtc };
    private static LocalSalesReturnEntity SalesEntity(SalesReturnModel m,DateTime n)=>new(){Id=m.LocalId,CompanyId=m.CompanyId,ReturnNumber=m.ReturnNumber,InvoiceId=m.InvoiceId,InvoiceNumber=m.InvoiceNumber,CustomerId=m.CustomerId,CustomerName=m.CustomerName,ReturnDate=m.ReturnDate.Date,ItemsJson=JsonSerializer.Serialize(m.Items),Reason=m.Reason.Trim(),Notes=m.Notes.Trim(),RefundAmount=m.RefundAmount,Status=(int)m.Status,SyncStatus=(int)RecordSyncStatus.Pending,CreatedAtUtc=n,UpdatedAtUtc=n};
    private static LocalPurchaseReturnEntity PurchaseEntity(PurchaseReturnModel m,DateTime n)=>new(){Id=m.LocalId,CompanyId=m.CompanyId,ReturnNumber=m.ReturnNumber,PurchaseId=m.PurchaseId,PurchaseNumber=m.PurchaseNumber,SupplierId=m.SupplierId,SupplierName=m.SupplierName,ReturnDate=m.ReturnDate.Date,ItemsJson=JsonSerializer.Serialize(m.Items),Reason=m.Reason.Trim(),Notes=m.Notes.Trim(),RefundAmount=m.RefundAmount,Status=(int)m.Status,SyncStatus=(int)RecordSyncStatus.Pending,CreatedAtUtc=n,UpdatedAtUtc=n};
    private static void Apply(LocalSalesReturnEntity x,SalesReturnModel m,DateTime n){x.InvoiceId=m.InvoiceId;x.InvoiceNumber=m.InvoiceNumber;x.CustomerId=m.CustomerId;x.CustomerName=m.CustomerName;x.ReturnDate=m.ReturnDate.Date;x.ItemsJson=JsonSerializer.Serialize(m.Items);x.Reason=m.Reason.Trim();x.Notes=m.Notes.Trim();x.RefundAmount=m.RefundAmount;x.Status=(int)m.Status;x.SyncStatus=(int)RecordSyncStatus.Pending;x.UpdatedAtUtc=n;x.SyncError=null;}
    private static void Apply(LocalPurchaseReturnEntity x,PurchaseReturnModel m,DateTime n){x.PurchaseId=m.PurchaseId;x.PurchaseNumber=m.PurchaseNumber;x.SupplierId=m.SupplierId;x.SupplierName=m.SupplierName;x.ReturnDate=m.ReturnDate.Date;x.ItemsJson=JsonSerializer.Serialize(m.Items);x.Reason=m.Reason.Trim();x.Notes=m.Notes.Trim();x.RefundAmount=m.RefundAmount;x.Status=(int)m.Status;x.SyncStatus=(int)RecordSyncStatus.Pending;x.UpdatedAtUtc=n;x.SyncError=null;}
    private static object SalesPayload(LocalSalesReturnEntity x)=>new{x.Id,x.CompanyId,x.ReturnNumber,x.InvoiceId,x.CustomerId,x.ReturnDate,x.ItemsJson,x.Reason,x.Notes,x.RefundAmount,x.Status};
    internal static object PurchasePayload(LocalPurchaseReturnEntity x)=>new{x.Id,x.CompanyId,x.ReturnNumber,x.PurchaseId,x.SupplierId,x.ReturnDate,x.ItemsJson,x.Reason,x.Notes,x.RefundAmount,x.Status,x.UpdatedAtUtc};
    private static SyncStatus ToUi(int s)=>(RecordSyncStatus)s==RecordSyncStatus.Synced?SyncStatus.Synced:(RecordSyncStatus)s==RecordSyncStatus.Failed?SyncStatus.SyncFailed:SyncStatus.PendingSync;
}
