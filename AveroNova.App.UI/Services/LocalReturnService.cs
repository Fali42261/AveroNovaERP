using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public sealed class LocalReturnService : IReturnService
{
    private const int ReturnMovementType = 4;
    private readonly IDbContextFactory<LocalAppDbContext> _factory;
    private readonly IAppSessionContext _session;
    private readonly IConnectivityService _connectivity;
    private readonly IReturnSyncService _sync;

    public LocalReturnService(IDbContextFactory<LocalAppDbContext> factory, IAppSessionContext session, IConnectivityService connectivity, IReturnSyncService sync)
    { _factory=factory; _session=session; _connectivity=connectivity; _sync=sync; }

    public async Task<List<SalesReturnModel>> GetSalesReturnsAsync(Guid companyId){if(!Allows(companyId))return[];await using var db=await _factory.CreateDbContextAsync();return(await db.SalesReturns.AsNoTracking().Where(x=>x.CompanyId==companyId).OrderByDescending(x=>x.ReturnDate).ToListAsync()).Select(MapSales).ToList();}
    public async Task<SalesReturnModel?> GetSalesReturnByIdAsync(Guid id){await using var db=await _factory.CreateDbContextAsync();var x=await db.SalesReturns.AsNoTracking().FirstOrDefaultAsync(r=>r.Id==id);return x is null||!Allows(x.CompanyId)?null:MapSales(x);}

    public async Task<(bool Ok,string? Error)> CreateSalesReturnAsync(SalesReturnModel m)
    {
        var err=await ValidateSalesAsync(m);if(err is not null)return(false,err);
        await using var db=await _factory.CreateDbContextAsync();await LocalSyncVersionStore.EnsureSchemaAsync(db);await using var tx=await db.Database.BeginTransactionAsync();
        var now=DateTime.UtcNow;m.LocalId=m.LocalId==Guid.Empty?Guid.NewGuid():m.LocalId;m.ReturnNumber=await NextAsync(db,"SR",m.CompanyId,true);var x=SalesEntity(m,now);db.SalesReturns.Add(x);
        var queueId=LocalSyncQueueWriter.Enqueue(db,"SalesReturn",x.Id,x.CompanyId,SyncOperation.Create,SalesPayload(x,1),now);
        err=await ApplyStockDeltaAsync(db,x.CompanyId,m.Items,1,queueId,x.ReturnNumber,"Sales return",now);if(err is not null)return(false,err);
        await db.SaveChangesAsync();await tx.CommitAsync();AfterQueued();return(true,null);
    }

    public async Task<(bool Ok,string? Error)> UpdateSalesReturnAsync(SalesReturnModel m)
    {
        var err=await ValidateSalesAsync(m);if(err is not null)return(false,err);
        await using var db=await _factory.CreateDbContextAsync();await LocalSyncVersionStore.EnsureSchemaAsync(db);await using var tx=await db.Database.BeginTransactionAsync();
        var x=await db.SalesReturns.FirstOrDefaultAsync(r=>r.Id==m.LocalId);if(x is null||!Allows(x.CompanyId)||x.CompanyId!=m.CompanyId)return(false,"Sales return not found.");
        var oldItems=DeserializeReturnItems(x.ItemsJson);var version=await LocalSyncVersionStore.GetSalesReturnAsync(db,x.Id);Apply(x,m,DateTime.UtcNow);
        var queueId=LocalSyncQueueWriter.Enqueue(db,"SalesReturn",x.Id,x.CompanyId,SyncOperation.Update,SalesPayload(x,version),x.UpdatedAtUtc);
        err=await ApplyStockDifferenceAsync(db,x.CompanyId,oldItems,m.Items,1,queueId,x.ReturnNumber,"Sales return updated",x.UpdatedAtUtc);if(err is not null)return(false,err);
        await db.SaveChangesAsync();await tx.CommitAsync();AfterQueued();return(true,null);
    }

    public async Task<(bool Ok,string? Error)> DeleteSalesReturnAsync(Guid id)
    {
        await using var db=await _factory.CreateDbContextAsync();await using var tx=await db.Database.BeginTransactionAsync();var x=await db.SalesReturns.FirstOrDefaultAsync(r=>r.Id==id);if(x is null||!Allows(x.CompanyId))return(false,"Sales return not found.");
        var items=DeserializeReturnItems(x.ItemsJson);var now=DateTime.UtcNow;var queueId=LocalSyncQueueWriter.Enqueue(db,"SalesReturn",x.Id,x.CompanyId,SyncOperation.Delete,new{x.Id,x.CompanyId},now);
        var err=await ApplyStockDeltaAsync(db,x.CompanyId,items,-1,queueId,x.ReturnNumber,"Sales return deleted",now);if(err is not null)return(false,err);
        db.SalesReturns.Remove(x);await db.SaveChangesAsync();await tx.CommitAsync();AfterQueued();return(true,null);
    }

    public async Task<List<PurchaseReturnModel>> GetPurchaseReturnsAsync(Guid companyId){if(!Allows(companyId))return[];await using var db=await _factory.CreateDbContextAsync();return(await db.PurchaseReturns.AsNoTracking().Where(x=>x.CompanyId==companyId).OrderByDescending(x=>x.ReturnDate).ToListAsync()).Select(MapPurchase).ToList();}
    public async Task<PurchaseReturnModel?> GetPurchaseReturnByIdAsync(Guid id){await using var db=await _factory.CreateDbContextAsync();var x=await db.PurchaseReturns.AsNoTracking().FirstOrDefaultAsync(r=>r.Id==id);return x is null||!Allows(x.CompanyId)?null:MapPurchase(x);}

    public async Task<(bool Ok,string? Error)> CreatePurchaseReturnAsync(PurchaseReturnModel m)
    {
        var err=await ValidatePurchaseAsync(m);if(err is not null)return(false,err);
        await using var db=await _factory.CreateDbContextAsync();await LocalSyncVersionStore.EnsureSchemaAsync(db);await using var tx=await db.Database.BeginTransactionAsync();
        var now=DateTime.UtcNow;m.LocalId=m.LocalId==Guid.Empty?Guid.NewGuid():m.LocalId;m.ReturnNumber=await NextAsync(db,"PR",m.CompanyId,false);var x=PurchaseEntity(m,now);db.PurchaseReturns.Add(x);
        var queueId=LocalSyncQueueWriter.Enqueue(db,"PurchaseReturn",x.Id,x.CompanyId,SyncOperation.Create,PurchasePayload(x,1),now);
        err=await ApplyStockDeltaAsync(db,x.CompanyId,m.Items,-1,queueId,x.ReturnNumber,"Purchase return",now);if(err is not null)return(false,err);
        await db.SaveChangesAsync();await tx.CommitAsync();AfterQueued();return(true,null);
    }

    public async Task<(bool Ok,string? Error)> UpdatePurchaseReturnAsync(PurchaseReturnModel m)
    {
        var err=await ValidatePurchaseAsync(m);if(err is not null)return(false,err);
        await using var db=await _factory.CreateDbContextAsync();await LocalSyncVersionStore.EnsureSchemaAsync(db);await using var tx=await db.Database.BeginTransactionAsync();
        var x=await db.PurchaseReturns.FirstOrDefaultAsync(r=>r.Id==m.LocalId);if(x is null||!Allows(x.CompanyId)||x.CompanyId!=m.CompanyId)return(false,"Purchase return not found.");
        var oldItems=DeserializeReturnItems(x.ItemsJson);var version=await LocalSyncVersionStore.GetPurchaseReturnAsync(db,x.Id);Apply(x,m,DateTime.UtcNow);
        var queueId=LocalSyncQueueWriter.Enqueue(db,"PurchaseReturn",x.Id,x.CompanyId,SyncOperation.Update,PurchasePayload(x,version),x.UpdatedAtUtc);
        err=await ApplyStockDifferenceAsync(db,x.CompanyId,oldItems,m.Items,-1,queueId,x.ReturnNumber,"Purchase return updated",x.UpdatedAtUtc);if(err is not null)return(false,err);
        await db.SaveChangesAsync();await tx.CommitAsync();AfterQueued();return(true,null);
    }

    public async Task<(bool Ok,string? Error)> DeletePurchaseReturnAsync(Guid id)
    {
        await using var db=await _factory.CreateDbContextAsync();await using var tx=await db.Database.BeginTransactionAsync();var x=await db.PurchaseReturns.FirstOrDefaultAsync(r=>r.Id==id);if(x is null||!Allows(x.CompanyId))return(false,"Purchase return not found.");
        var items=DeserializeReturnItems(x.ItemsJson);var now=DateTime.UtcNow;var queueId=LocalSyncQueueWriter.Enqueue(db,"PurchaseReturn",x.Id,x.CompanyId,SyncOperation.Delete,new{x.Id,x.CompanyId},now);
        var err=await ApplyStockDeltaAsync(db,x.CompanyId,items,1,queueId,x.ReturnNumber,"Purchase return deleted",now);if(err is not null)return(false,err);
        db.PurchaseReturns.Remove(x);await db.SaveChangesAsync();await tx.CommitAsync();AfterQueued();return(true,null);
    }

    private async Task<string?> ApplyStockDifferenceAsync(LocalAppDbContext db,Guid companyId,List<ReturnLineItem> oldItems,List<ReturnLineItem> newItems,int direction,Guid queueId,string reference,string notes,DateTime now)
    {
        var oldQty=oldItems.GroupBy(x=>x.ProductId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));var newQty=newItems.GroupBy(x=>x.ProductId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));
        var deltas=oldQty.Keys.Union(newQty.Keys).Select(id=>new ReturnLineItem{ProductId=id,Quantity=(newQty.GetValueOrDefault(id)-oldQty.GetValueOrDefault(id))*direction}).Where(x=>x.Quantity!=0).ToList();
        return await ApplySignedStockDeltaAsync(db,companyId,deltas,queueId,reference,notes,now);
    }

    private Task<string?> ApplyStockDeltaAsync(LocalAppDbContext db,Guid companyId,List<ReturnLineItem> items,int direction,Guid queueId,string reference,string notes,DateTime now)
        =>ApplySignedStockDeltaAsync(db,companyId,items.GroupBy(x=>x.ProductId).Select(g=>new ReturnLineItem{ProductId=g.Key,ProductName=g.First().ProductName,Quantity=g.Sum(x=>x.Quantity)*direction}).ToList(),queueId,reference,notes,now);

    private async Task<string?> ApplySignedStockDeltaAsync(LocalAppDbContext db,Guid companyId,List<ReturnLineItem> deltas,Guid queueId,string reference,string notes,DateTime now)
    {
        foreach(var delta in deltas)
        {
            if(delta.ProductId==Guid.Empty||delta.Quantity==0)continue;
            var product=await db.Products.FirstOrDefaultAsync(x=>x.Id==delta.ProductId&&x.CompanyId==companyId);if(product is null)return $"Product {delta.ProductName} was not found in inventory.";
            var before=product.Stock;var after=before+delta.Quantity;if(after<0)return $"Insufficient stock for {product.Name}.";
            product.Stock=after;product.UpdatedAtUtc=now;
            db.StockMovements.Add(new LocalStockMovementEntity{Id=Guid.NewGuid(),CompanyId=companyId,ProductId=product.Id,ProductName=product.Name,SKU=product.SKU,Type=ReturnMovementType,Quantity=delta.Quantity,StockBefore=before,StockAfter=after,Reference=$"RETURNQ:{queueId:D}",Notes=$"{notes} {reference}".Trim(),CreatedBy="Return",SyncStatus=(int)RecordSyncStatus.Pending,CreatedAtUtc=now,UpdatedAtUtc=now});
        }
        return null;
    }

    private void AfterQueued(){_connectivity.IncrementPending();if(_connectivity.IsOnline)_=_sync.SyncPendingAsync();}

    private async Task<string?> ValidateSalesAsync(SalesReturnModel m)
    {
        var common=Validate(m.CompanyId,m.ReturnDate,m.Reason,m.RefundAmount,m.Items);if(common is not null)return common;if(m.InvoiceId==Guid.Empty)return"Select an invoice.";await using var db=await _factory.CreateDbContextAsync();var invoice=await db.Invoices.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==m.InvoiceId&&x.CompanyId==m.CompanyId);if(invoice is null)return"Invoice not found for this company.";
        var items=JsonSerializer.Deserialize<List<InvoiceLineItem>>(invoice.ItemsJson)??[];var source=items.GroupBy(x=>x.ProductId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));foreach(var item in m.Items){if(!source.TryGetValue(item.ProductId,out var qty))return"A returned product does not exist on the invoice.";if(item.Quantity>qty)return"Returned quantity cannot exceed the invoiced quantity.";}
        var subtotal=items.Sum(x=>x.LineTotal);var total=subtotal+items.Sum(x=>x.TaxAmount)+subtotal*invoice.TaxPct/100m-subtotal*invoice.DiscountPct/100m;if(m.RefundAmount>Math.Max(0,total))return"Refund cannot exceed invoice total.";m.InvoiceNumber=invoice.InvoiceNumber;m.CustomerId=invoice.CustomerId;m.CustomerName=invoice.CustomerName;return null;
    }

    private async Task<string?> ValidatePurchaseAsync(PurchaseReturnModel m)
    {
        var common=Validate(m.CompanyId,m.ReturnDate,m.Reason,m.RefundAmount,m.Items);if(common is not null)return common;if(m.PurchaseId==Guid.Empty)return"Select a purchase.";await using var db=await _factory.CreateDbContextAsync();var purchase=await db.Purchases.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==m.PurchaseId&&x.CompanyId==m.CompanyId);if(purchase is null)return"Purchase not found for this company.";
        var items=JsonSerializer.Deserialize<List<PurchaseLineItem>>(purchase.ItemsJson)??[];var source=items.GroupBy(x=>x.ProductId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));foreach(var item in m.Items){if(!source.TryGetValue(item.ProductId,out var qty))return"A returned product does not exist on the purchase.";if(item.Quantity>qty)return"Returned quantity cannot exceed the purchased quantity.";}
        var total=items.Sum(x=>x.GrandTotal);if(m.RefundAmount>total)return"Refund cannot exceed purchase total.";m.PurchaseNumber=purchase.PurchaseNumber;m.SupplierId=purchase.SupplierId;m.SupplierName=purchase.SupplierName;return null;
    }

    private string? Validate(Guid companyId,DateTime date,string reason,decimal refund,List<ReturnLineItem> items){if(!Allows(companyId))return"You do not have access to this company.";if(date.Date>DateTime.Today)return"Return date cannot be in the future.";if(string.IsNullOrWhiteSpace(reason))return"Return reason is required.";if(refund<=0)return"Refund amount must be greater than zero.";if(items.Count==0)return"At least one return item is required.";if(items.Any(x=>x.ProductId==Guid.Empty||x.Quantity<=0))return"Every returned item must have a product and positive quantity.";return null;}
    private async Task<string> NextAsync(LocalAppDbContext db,string kind,Guid cid,bool sales){var prefix=$"{kind}-{DateTime.Today:yyyy}-";var values=sales?await db.SalesReturns.Where(x=>x.CompanyId==cid&&x.ReturnNumber.StartsWith(prefix)).Select(x=>x.ReturnNumber).ToListAsync():await db.PurchaseReturns.Where(x=>x.CompanyId==cid&&x.ReturnNumber.StartsWith(prefix)).Select(x=>x.ReturnNumber).ToListAsync();var max=values.Select(x=>int.TryParse(x[prefix.Length..],out var n)?n:0).DefaultIfEmpty().Max();return$"{prefix}{max+1:D4}";}
    private bool Allows(Guid id)=>id!=Guid.Empty&&_session.CurrentCompanyId==id;
    private static List<ReturnLineItem> DeserializeReturnItems(string json){try{return JsonSerializer.Deserialize<List<ReturnLineItem>>(json)??[];}catch{return[];}}
    private static SalesReturnModel MapSales(LocalSalesReturnEntity x)=>new(){LocalId=x.Id,ServerId=x.ServerId?.ToString("D"),CompanyId=x.CompanyId,ReturnNumber=x.ReturnNumber,InvoiceId=x.InvoiceId,InvoiceNumber=x.InvoiceNumber,CustomerId=x.CustomerId,CustomerName=x.CustomerName,ReturnDate=x.ReturnDate,Items=DeserializeReturnItems(x.ItemsJson),Reason=x.Reason,Notes=x.Notes,RefundAmount=x.RefundAmount,Status=(ReturnStatus)x.Status,SyncStatus=ToUi(x.SyncStatus),CreatedAt=x.CreatedAtUtc,UpdatedAt=x.UpdatedAtUtc,LastSyncedAt=x.LastSyncedAtUtc};
    private static PurchaseReturnModel MapPurchase(LocalPurchaseReturnEntity x)=>new(){LocalId=x.Id,ServerId=x.ServerId?.ToString("D"),CompanyId=x.CompanyId,ReturnNumber=x.ReturnNumber,PurchaseId=x.PurchaseId,PurchaseNumber=x.PurchaseNumber,SupplierId=x.SupplierId,SupplierName=x.SupplierName,ReturnDate=x.ReturnDate,Items=DeserializeReturnItems(x.ItemsJson),Reason=x.Reason,Notes=x.Notes,RefundAmount=x.RefundAmount,Status=(ReturnStatus)x.Status,SyncStatus=ToUi(x.SyncStatus),CreatedAt=x.CreatedAtUtc,UpdatedAt=x.UpdatedAtUtc,LastSyncedAt=x.LastSyncedAtUtc};
    private static LocalSalesReturnEntity SalesEntity(SalesReturnModel m,DateTime n)=>new(){Id=m.LocalId,CompanyId=m.CompanyId,ReturnNumber=m.ReturnNumber,InvoiceId=m.InvoiceId,InvoiceNumber=m.InvoiceNumber,CustomerId=m.CustomerId,CustomerName=m.CustomerName,ReturnDate=m.ReturnDate.Date,ItemsJson=JsonSerializer.Serialize(m.Items),Reason=m.Reason.Trim(),Notes=m.Notes.Trim(),RefundAmount=m.RefundAmount,Status=(int)m.Status,SyncStatus=(int)RecordSyncStatus.Pending,CreatedAtUtc=n,UpdatedAtUtc=n};
    private static LocalPurchaseReturnEntity PurchaseEntity(PurchaseReturnModel m,DateTime n)=>new(){Id=m.LocalId,CompanyId=m.CompanyId,ReturnNumber=m.ReturnNumber,PurchaseId=m.PurchaseId,PurchaseNumber=m.PurchaseNumber,SupplierId=m.SupplierId,SupplierName=m.SupplierName,ReturnDate=m.ReturnDate.Date,ItemsJson=JsonSerializer.Serialize(m.Items),Reason=m.Reason.Trim(),Notes=m.Notes.Trim(),RefundAmount=m.RefundAmount,Status=(int)m.Status,SyncStatus=(int)RecordSyncStatus.Pending,CreatedAtUtc=n,UpdatedAtUtc=n};
    private static void Apply(LocalSalesReturnEntity x,SalesReturnModel m,DateTime n){x.InvoiceId=m.InvoiceId;x.InvoiceNumber=m.InvoiceNumber;x.CustomerId=m.CustomerId;x.CustomerName=m.CustomerName;x.ReturnDate=m.ReturnDate.Date;x.ItemsJson=JsonSerializer.Serialize(m.Items);x.Reason=m.Reason.Trim();x.Notes=m.Notes.Trim();x.RefundAmount=m.RefundAmount;x.Status=(int)m.Status;x.SyncStatus=(int)RecordSyncStatus.Pending;x.UpdatedAtUtc=n;x.SyncError=null;}
    private static void Apply(LocalPurchaseReturnEntity x,PurchaseReturnModel m,DateTime n){x.PurchaseId=m.PurchaseId;x.PurchaseNumber=m.PurchaseNumber;x.SupplierId=m.SupplierId;x.SupplierName=m.SupplierName;x.ReturnDate=m.ReturnDate.Date;x.ItemsJson=JsonSerializer.Serialize(m.Items);x.Reason=m.Reason.Trim();x.Notes=m.Notes.Trim();x.RefundAmount=m.RefundAmount;x.Status=(int)m.Status;x.SyncStatus=(int)RecordSyncStatus.Pending;x.UpdatedAtUtc=n;x.SyncError=null;}
    private static object SalesPayload(LocalSalesReturnEntity x,long syncVersion)=>new{x.Id,x.CompanyId,x.ReturnNumber,x.InvoiceId,x.InvoiceNumber,x.CustomerId,x.CustomerName,x.ReturnDate,x.ItemsJson,x.Reason,x.Notes,x.RefundAmount,x.Status,SyncVersion=Math.Max(1L,syncVersion)};
    private static object PurchasePayload(LocalPurchaseReturnEntity x,long syncVersion)=>new{x.Id,x.CompanyId,x.ReturnNumber,x.PurchaseId,x.PurchaseNumber,x.SupplierId,x.SupplierName,x.ReturnDate,x.ItemsJson,x.Reason,x.Notes,x.RefundAmount,x.Status,SyncVersion=Math.Max(1L,syncVersion)};
    private static SyncStatus ToUi(int s)=>(RecordSyncStatus)s==RecordSyncStatus.Synced?SyncStatus.Synced:(RecordSyncStatus)s==RecordSyncStatus.Failed?SyncStatus.SyncFailed:SyncStatus.PendingSync;
}
