using System.Security.Claims;
using System.Text.Json;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.API.Controllers;

[ApiController]
[Route("api/returns")]
[Authorize]
public sealed class ReturnsController(AppDbContext db) : ControllerBase
{
    private const int ReturnMovementType = 4;
    private const int RejectedStatus = 2;
    private const int CompletedStatus = 3;

    [HttpGet("sales/company/{companyId:guid}")]
    public async Task<IActionResult> GetSales(Guid companyId, CancellationToken ct)
    {
        if (!await CanAccessCompany(companyId, ct)) return Forbid();
        return Ok(new { success = true, data = await db.SalesReturns.AsNoTracking().Where(x => x.CompanyId == companyId && !x.IsDeleted).OrderByDescending(x => x.ReturnDate).ToListAsync(ct) });
    }

    [HttpGet("purchase/company/{companyId:guid}")]
    public async Task<IActionResult> GetPurchase(Guid companyId, CancellationToken ct)
    {
        if (!await CanAccessCompany(companyId, ct)) return Forbid();
        return Ok(new { success = true, data = await db.PurchaseReturns.AsNoTracking().Where(x => x.CompanyId == companyId && !x.IsDeleted).OrderByDescending(x => x.ReturnDate).ToListAsync(ct) });
    }

    [HttpPost("sales")]
    public async Task<IActionResult> CreateSales([FromBody] SalesReturnRequest r, CancellationToken ct)
    {
        if (!await CanAccessCompany(r.CompanyId, ct)) return Forbid();
        var existing = await db.SalesReturns.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (existing is not null)
        {
            if (existing.CompanyId != r.CompanyId) return Conflict(new { success = false, error = "Return id already belongs to another company." });
            return Ok(new { success = true, data = ToResponse(existing), idempotent = true });
        }
        var error = await ValidateSales(r, null, ct); if (error is not null) return BadRequest(new { success = false, error });
        if (await db.SalesReturns.AnyAsync(x => x.CompanyId == r.CompanyId && x.ReturnNumber == r.ReturnNumber && !x.IsDeleted, ct)) return Conflict(new { success = false, error = "Sales return number already exists." });

        TryReadReturnItems(r.ItemsJson, out var returned, out _);
        var now = DateTime.UtcNow;
        if (IsInventoryApplied(r.Status))
        {
            var stockError = await ApplyStockDeltaAsync(r.CompanyId, returned, 1, r.ReturnNumber, "Sales return completed", now, ct);
            if (stockError is not null) return BadRequest(new { success = false, error = stockError });
        }

        var row = new SalesReturn { Id=r.Id, CompanyId=r.CompanyId, ReturnNumber=r.ReturnNumber.Trim(), InvoiceId=r.InvoiceId, InvoiceNumber=r.InvoiceNumber?.Trim() ?? "", CustomerId=r.CustomerId, CustomerName=r.CustomerName?.Trim() ?? "", ReturnDate=r.ReturnDate, ItemsJson=string.IsNullOrWhiteSpace(r.ItemsJson)?"[]":r.ItemsJson, Reason=r.Reason.Trim(), Notes=r.Notes?.Trim() ?? "", RefundAmount=r.RefundAmount, Status=r.Status, CreatedAt=now, UpdatedAt=now, SyncVersion=Math.Max(1,r.SyncVersion), SyncStatus=RecordSyncStatus.Synced, LastSyncedAt=now };
        db.SalesReturns.Add(row); await db.SaveChangesAsync(ct); return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpPut("sales/{id:guid}")]
    public async Task<IActionResult> UpdateSales(Guid id, [FromBody] SalesReturnRequest r, CancellationToken ct)
    {
        var row = await db.SalesReturns.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct); if (row is null) return NotFound(new { success=false,error="Sales return not found." });
        if (!await CanAccessCompany(row.CompanyId, ct)) return Forbid();
        if (r.CompanyId != row.CompanyId) return BadRequest(new { success=false,error="Return company cannot be changed." });
        var error = await ValidateSales(r, row.Id, ct); if (error is not null) return BadRequest(new { success=false,error });
        if (r.SyncVersion > 0 && r.SyncVersion < row.SyncVersion) return Conflict(new { success=false,error="Sales return has newer server changes." });

        TryReadReturnItems(row.ItemsJson, out var oldItems, out _); TryReadReturnItems(r.ItemsJson, out var newItems, out _);
        var oldEffective = IsInventoryApplied(row.Status) ? oldItems : [];
        var newEffective = IsInventoryApplied(r.Status) ? newItems : [];
        var now=DateTime.UtcNow; var stockError=await ApplyStockDifferenceAsync(row.CompanyId,oldEffective,newEffective,1,row.ReturnNumber,"Sales return updated",now,ct);
        if(stockError is not null)return BadRequest(new{success=false,error=stockError});

        row.ApplyUpdate(new SalesReturn { InvoiceId=r.InvoiceId,InvoiceNumber=r.InvoiceNumber??"",CustomerId=r.CustomerId,CustomerName=r.CustomerName??"",ReturnDate=r.ReturnDate,ItemsJson=r.ItemsJson??"[]",Reason=r.Reason,Notes=r.Notes??"",RefundAmount=r.RefundAmount,Status=r.Status });
        row.SyncStatus=RecordSyncStatus.Synced; row.LastSyncedAt=now; await db.SaveChangesAsync(ct); return Ok(new { success=true,data=ToResponse(row) });
    }

    [HttpDelete("sales/{id:guid}")]
    public async Task<IActionResult> DeleteSales(Guid id, CancellationToken ct)
    {
        var row=await db.SalesReturns.FirstOrDefaultAsync(x=>x.Id==id&&!x.IsDeleted,ct); if(row is null)return Ok(new{success=true}); if(!await CanAccessCompany(row.CompanyId,ct))return Forbid();
        var now=DateTime.UtcNow;
        if(IsInventoryApplied(row.Status))
        {
            TryReadReturnItems(row.ItemsJson,out var items,out _);var stockError=await ApplyStockDeltaAsync(row.CompanyId,items,-1,row.ReturnNumber,"Sales return deleted",now,ct);if(stockError is not null)return BadRequest(new{success=false,error=stockError});
        }
        row.IsDeleted=true; row.MarkPendingChange(); row.SyncStatus=RecordSyncStatus.Synced; row.LastSyncedAt=now; await db.SaveChangesAsync(ct); return Ok(new{success=true});
    }

    [HttpPost("purchase")]
    public async Task<IActionResult> CreatePurchase([FromBody] PurchaseReturnRequest r, CancellationToken ct)
    {
        if (!await CanAccessCompany(r.CompanyId, ct)) return Forbid();
        var existing=await db.PurchaseReturns.FirstOrDefaultAsync(x=>x.Id==r.Id,ct);
        if(existing is not null)
        {
            if(existing.CompanyId!=r.CompanyId)return Conflict(new{success=false,error="Return id already belongs to another company."});
            return Ok(new{success=true,data=ToResponse(existing),idempotent=true});
        }
        var error = await ValidatePurchase(r, null, ct); if (error is not null) return BadRequest(new { success=false,error });
        if(await db.PurchaseReturns.AnyAsync(x=>x.CompanyId==r.CompanyId&&x.ReturnNumber==r.ReturnNumber&&!x.IsDeleted,ct))return Conflict(new{success=false,error="Purchase return number already exists."});

        TryReadReturnItems(r.ItemsJson,out var returned,out _);var now=DateTime.UtcNow;
        if(IsInventoryApplied(r.Status))
        {
            var stockError=await ApplyStockDeltaAsync(r.CompanyId,returned,-1,r.ReturnNumber,"Purchase return completed",now,ct);if(stockError is not null)return BadRequest(new{success=false,error=stockError});
        }
        var row=new PurchaseReturn{Id=r.Id,CompanyId=r.CompanyId,ReturnNumber=r.ReturnNumber.Trim(),PurchaseId=r.PurchaseId,PurchaseNumber=r.PurchaseNumber?.Trim()??"",SupplierId=r.SupplierId,SupplierName=r.SupplierName?.Trim()??"",ReturnDate=r.ReturnDate,ItemsJson=string.IsNullOrWhiteSpace(r.ItemsJson)?"[]":r.ItemsJson,Reason=r.Reason.Trim(),Notes=r.Notes?.Trim()??"",RefundAmount=r.RefundAmount,Status=r.Status,CreatedAt=now,UpdatedAt=now,SyncVersion=Math.Max(1,r.SyncVersion),SyncStatus=RecordSyncStatus.Synced,LastSyncedAt=now};
        db.PurchaseReturns.Add(row); await db.SaveChangesAsync(ct); return Ok(new{success=true,data=ToResponse(row)});
    }

    [HttpPut("purchase/{id:guid}")]
    public async Task<IActionResult> UpdatePurchase(Guid id,[FromBody] PurchaseReturnRequest r,CancellationToken ct)
    {
        var row=await db.PurchaseReturns.FirstOrDefaultAsync(x=>x.Id==id&&!x.IsDeleted,ct); if(row is null)return NotFound(new{success=false,error="Purchase return not found."}); if(!await CanAccessCompany(row.CompanyId,ct))return Forbid();
        if(r.CompanyId!=row.CompanyId)return BadRequest(new{success=false,error="Return company cannot be changed."});
        var error=await ValidatePurchase(r,row.Id,ct); if(error is not null)return BadRequest(new{success=false,error}); if(r.SyncVersion>0&&r.SyncVersion<row.SyncVersion)return Conflict(new{success=false,error="Purchase return has newer server changes."});
        TryReadReturnItems(row.ItemsJson,out var oldItems,out _);TryReadReturnItems(r.ItemsJson,out var newItems,out _);
        var oldEffective=IsInventoryApplied(row.Status)?oldItems:[];var newEffective=IsInventoryApplied(r.Status)?newItems:[];
        var now=DateTime.UtcNow;var stockError=await ApplyStockDifferenceAsync(row.CompanyId,oldEffective,newEffective,-1,row.ReturnNumber,"Purchase return updated",now,ct);if(stockError is not null)return BadRequest(new{success=false,error=stockError});
        row.ApplyUpdate(new PurchaseReturn{PurchaseId=r.PurchaseId,PurchaseNumber=r.PurchaseNumber??"",SupplierId=r.SupplierId,SupplierName=r.SupplierName??"",ReturnDate=r.ReturnDate,ItemsJson=r.ItemsJson??"[]",Reason=r.Reason,Notes=r.Notes??"",RefundAmount=r.RefundAmount,Status=r.Status}); row.SyncStatus=RecordSyncStatus.Synced; row.LastSyncedAt=now; await db.SaveChangesAsync(ct); return Ok(new{success=true,data=ToResponse(row)});
    }

    [HttpDelete("purchase/{id:guid}")]
    public async Task<IActionResult> DeletePurchase(Guid id,CancellationToken ct)
    {
        var row=await db.PurchaseReturns.FirstOrDefaultAsync(x=>x.Id==id&&!x.IsDeleted,ct); if(row is null)return Ok(new{success=true}); if(!await CanAccessCompany(row.CompanyId,ct))return Forbid();
        var now=DateTime.UtcNow;
        if(IsInventoryApplied(row.Status))
        {
            TryReadReturnItems(row.ItemsJson,out var items,out _);var stockError=await ApplyStockDeltaAsync(row.CompanyId,items,1,row.ReturnNumber,"Purchase return deleted",now,ct);if(stockError is not null)return BadRequest(new{success=false,error=stockError});
        }
        row.IsDeleted=true; row.MarkPendingChange(); row.SyncStatus=RecordSyncStatus.Synced; row.LastSyncedAt=now; await db.SaveChangesAsync(ct); return Ok(new{success=true});
    }

    private async Task<string?> ValidateSales(SalesReturnRequest r,Guid? excludeId,CancellationToken ct)
    {
        if(r.Id==Guid.Empty||r.CompanyId==Guid.Empty||r.InvoiceId==Guid.Empty||string.IsNullOrWhiteSpace(r.ReturnNumber)||string.IsNullOrWhiteSpace(r.Reason)||r.RefundAmount<=0)
            return "Return id, company, invoice, number, reason and positive refund are required.";
        if(r.Status<0||r.Status>CompletedStatus)return "Return status is invalid.";
        if(r.ReturnDate.Date>DateTime.UtcNow.Date)return "Return date cannot be in the future.";
        var invoice=await db.Invoices.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==r.InvoiceId&&x.CompanyId==r.CompanyId&&!x.IsDeleted,ct);if(invoice is null)return "Invoice not found for this company.";
        if(!TryReadReturnItems(r.ItemsJson,out var returned,out var itemError))return itemError;if(!TryReadInvoiceItems(invoice.ItemsJson,out var source))return "Invoice items are invalid on the server.";
        var sourceByProduct=source.GroupBy(x=>x.ProductId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));var returnedByProduct=returned.GroupBy(x=>x.ProductId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));
        foreach(var item in returnedByProduct){if(item.Key==Guid.Empty||item.Value<=0)return "Every returned item must have a product and positive quantity.";if(!sourceByProduct.TryGetValue(item.Key,out var sourceQty))return "A returned product does not exist on the invoice.";if(item.Value>sourceQty)return "Returned quantity cannot exceed the invoiced quantity.";}
        var subtotal=source.Sum(x=>x.UnitPrice*x.Quantity*(1-x.DiscountPct/100m));var lineTax=source.Sum(x=>(x.UnitPrice*x.Quantity*(1-x.DiscountPct/100m))*x.TaxPct/100m);var invoiceTax=subtotal*invoice.TaxPct/100m;var invoiceDiscount=subtotal*invoice.DiscountPct/100m;var invoiceTotal=Math.Max(0m,subtotal+lineTax+invoiceTax-invoiceDiscount);if(r.RefundAmount>invoiceTotal)return "Refund cannot exceed invoice total.";
        if(r.Status!=RejectedStatus)
        {
            var prior=await db.SalesReturns.AsNoTracking().Where(x=>x.CompanyId==r.CompanyId&&x.InvoiceId==r.InvoiceId&&!x.IsDeleted&&x.Status!=RejectedStatus&&(!excludeId.HasValue||x.Id!=excludeId.Value)).Select(x=>new{x.ItemsJson,x.RefundAmount}).ToListAsync(ct);
            var priorQty=new Dictionary<Guid,int>();foreach(var p in prior){if(!TryReadReturnItems(p.ItemsJson,out var pItems,out _))continue;foreach(var g in pItems.GroupBy(x=>x.ProductId))priorQty[g.Key]=priorQty.GetValueOrDefault(g.Key)+g.Sum(x=>x.Quantity);}
            foreach(var item in returnedByProduct){if(priorQty.GetValueOrDefault(item.Key)+item.Value>sourceByProduct[item.Key])return "Total returned quantity across returns cannot exceed the invoiced quantity.";}
            if(prior.Sum(x=>x.RefundAmount)+r.RefundAmount>invoiceTotal)return "Total refund across returns cannot exceed invoice total.";
        }
        return null;
    }

    private async Task<string?> ValidatePurchase(PurchaseReturnRequest r,Guid? excludeId,CancellationToken ct)
    {
        if(r.Id==Guid.Empty||r.CompanyId==Guid.Empty||r.PurchaseId==Guid.Empty||string.IsNullOrWhiteSpace(r.ReturnNumber)||string.IsNullOrWhiteSpace(r.Reason)||r.RefundAmount<=0)
            return "Return id, company, purchase, number, reason and positive refund are required.";
        if(r.Status<0||r.Status>CompletedStatus)return "Return status is invalid.";
        if(r.ReturnDate.Date>DateTime.UtcNow.Date)return "Return date cannot be in the future.";
        var purchase=await db.Purchases.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==r.PurchaseId&&x.CompanyId==r.CompanyId&&!x.IsDeleted,ct);if(purchase is null)return "Purchase not found for this company.";
        if(!TryReadReturnItems(r.ItemsJson,out var returned,out var itemError))return itemError;if(!TryReadPurchaseItems(purchase.ItemsJson,out var source))return "Purchase items are invalid on the server.";
        var sourceByProduct=source.GroupBy(x=>x.ProductId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));var returnedByProduct=returned.GroupBy(x=>x.ProductId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));
        foreach(var item in returnedByProduct){if(item.Key==Guid.Empty||item.Value<=0)return "Every returned item must have a product and positive quantity.";if(!sourceByProduct.TryGetValue(item.Key,out var sourceQty))return "A returned product does not exist on the purchase.";if(item.Value>sourceQty)return "Returned quantity cannot exceed the purchased quantity.";}
        var purchaseTotal=source.Sum(x=>(x.UnitPrice*x.Quantity)*(1+x.TaxPct/100m));if(r.RefundAmount>purchaseTotal)return "Refund cannot exceed purchase total.";
        if(r.Status!=RejectedStatus)
        {
            var prior=await db.PurchaseReturns.AsNoTracking().Where(x=>x.CompanyId==r.CompanyId&&x.PurchaseId==r.PurchaseId&&!x.IsDeleted&&x.Status!=RejectedStatus&&(!excludeId.HasValue||x.Id!=excludeId.Value)).Select(x=>new{x.ItemsJson,x.RefundAmount}).ToListAsync(ct);
            var priorQty=new Dictionary<Guid,int>();foreach(var p in prior){if(!TryReadReturnItems(p.ItemsJson,out var pItems,out _))continue;foreach(var g in pItems.GroupBy(x=>x.ProductId))priorQty[g.Key]=priorQty.GetValueOrDefault(g.Key)+g.Sum(x=>x.Quantity);}
            foreach(var item in returnedByProduct){if(priorQty.GetValueOrDefault(item.Key)+item.Value>sourceByProduct[item.Key])return "Total returned quantity across returns cannot exceed the purchased quantity.";}
            if(prior.Sum(x=>x.RefundAmount)+r.RefundAmount>purchaseTotal)return "Total refund across returns cannot exceed purchase total.";
        }
        return null;
    }

    private static bool IsInventoryApplied(int status)=>status==CompletedStatus;

    private async Task<string?> ApplyStockDifferenceAsync(Guid companyId,List<ReturnItemDto> oldItems,List<ReturnItemDto> newItems,int direction,string reference,string notes,DateTime now,CancellationToken ct)
    {
        var oldQty=oldItems.GroupBy(x=>x.ProductId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));var newQty=newItems.GroupBy(x=>x.ProductId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));
        var deltas=oldQty.Keys.Union(newQty.Keys).Select(id=>new ReturnItemDto{ProductId=id,Quantity=(newQty.GetValueOrDefault(id)-oldQty.GetValueOrDefault(id))*direction}).Where(x=>x.Quantity!=0).ToList();
        return await ApplySignedStockDeltaAsync(companyId,deltas,reference,notes,now,ct);
    }

    private Task<string?> ApplyStockDeltaAsync(Guid companyId,List<ReturnItemDto> items,int direction,string reference,string notes,DateTime now,CancellationToken ct)
        =>ApplySignedStockDeltaAsync(companyId,items.GroupBy(x=>x.ProductId).Select(g=>new ReturnItemDto{ProductId=g.Key,Quantity=g.Sum(x=>x.Quantity)*direction}).ToList(),reference,notes,now,ct);

    private async Task<string?> ApplySignedStockDeltaAsync(Guid companyId,List<ReturnItemDto> deltas,string reference,string notes,DateTime now,CancellationToken ct)
    {
        foreach(var delta in deltas)
        {
            if(delta.ProductId==Guid.Empty||delta.Quantity==0)continue;
            var product=await db.Products.FirstOrDefaultAsync(x=>x.Id==delta.ProductId&&x.CompanyId==companyId&&!x.IsDeleted,ct);if(product is null)return "A return product was not found in inventory.";
            var before=product.Stock;var after=before+delta.Quantity;if(after<0)return $"Insufficient stock for {product.Name}.";
            product.Stock=after;product.UpdatedAt=now;product.SyncVersion=Math.Max(1,product.SyncVersion+1);product.SyncStatus=RecordSyncStatus.Synced;product.LastSyncedAt=now;
            db.StockMovements.Add(new StockMovement{Id=Guid.NewGuid(),CompanyId=companyId,ProductId=product.Id,ProductName=product.Name,SKU=product.SKU,Type=ReturnMovementType,Quantity=delta.Quantity,StockBefore=before,StockAfter=after,Reference=reference.Trim(),Notes=notes,CreatedBy="Return",CreatedAt=now,UpdatedAt=now,SyncVersion=1,SyncStatus=RecordSyncStatus.Synced,LastSyncedAt=now});
        }
        return null;
    }

    private static bool TryReadReturnItems(string? json,out List<ReturnItemDto> items,out string error){items=[];error="";if(string.IsNullOrWhiteSpace(json)){error="At least one return item is required.";return false;}try{items=JsonSerializer.Deserialize<List<ReturnItemDto>>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[];}catch(JsonException){error="Return items are invalid.";return false;}if(items.Count==0){error="At least one return item is required.";return false;}return true;}
    private static bool TryReadInvoiceItems(string? json,out List<InvoiceItemDto> items){try{items=JsonSerializer.Deserialize<List<InvoiceItemDto>>(json??"[]",new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[];return items.Count>0;}catch(JsonException){items=[];return false;}}
    private static bool TryReadPurchaseItems(string? json,out List<PurchaseItemDto> items){try{items=JsonSerializer.Deserialize<List<PurchaseItemDto>>(json??"[]",new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[];return items.Count>0;}catch(JsonException){items=[];return false;}}
    private async Task<bool> CanAccessCompany(Guid companyId,CancellationToken ct){var raw=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");return Guid.TryParse(raw,out var userId)&&await db.UserCompanies.AnyAsync(x=>x.UserId==userId&&x.CompanyId==companyId&&x.IsActive&&!x.IsDeleted,ct);}
    private static object ToResponse(BaseEntity x)=>new{id=x.Id,x.SyncVersion,x.UpdatedAt};

    private sealed class ReturnItemDto{public Guid ProductId{get;set;}public int Quantity{get;set;}public decimal UnitPrice{get;set;}}
    private sealed class InvoiceItemDto{public Guid ProductId{get;set;}public int Quantity{get;set;}public decimal UnitPrice{get;set;}public decimal DiscountPct{get;set;}public decimal TaxPct{get;set;}}
    private sealed class PurchaseItemDto{public Guid ProductId{get;set;}public int Quantity{get;set;}public decimal UnitPrice{get;set;}public decimal TaxPct{get;set;}}

    public sealed class SalesReturnRequest{public Guid Id{get;set;}public Guid CompanyId{get;set;}public string ReturnNumber{get;set;}="";public Guid InvoiceId{get;set;}public string? InvoiceNumber{get;set;}public Guid CustomerId{get;set;}public string? CustomerName{get;set;}public DateTime ReturnDate{get;set;}public string? ItemsJson{get;set;}public string Reason{get;set;}="";public string? Notes{get;set;}public decimal RefundAmount{get;set;}public int Status{get;set;}public long SyncVersion{get;set;}}
    public sealed class PurchaseReturnRequest{public Guid Id{get;set;}public Guid CompanyId{get;set;}public string ReturnNumber{get;set;}="";public Guid PurchaseId{get;set;}public string? PurchaseNumber{get;set;}public Guid SupplierId{get;set;}public string? SupplierName{get;set;}public DateTime ReturnDate{get;set;}public string? ItemsJson{get;set;}public string Reason{get;set;}="";public string? Notes{get;set;}public decimal RefundAmount{get;set;}public int Status{get;set;}public long SyncVersion{get;set;}}
}
