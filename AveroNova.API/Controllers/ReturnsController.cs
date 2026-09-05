using System.Security.Claims;
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
        var error = await ValidateSales(r, ct); if (error is not null) return BadRequest(new { success = false, error });
        var existing = await db.SalesReturns.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (existing is not null) return Ok(new { success = true, data = ToResponse(existing), idempotent = true });
        if (await db.SalesReturns.AnyAsync(x => x.CompanyId == r.CompanyId && x.ReturnNumber == r.ReturnNumber && !x.IsDeleted, ct)) return Conflict(new { success = false, error = "Sales return number already exists." });
        var now = DateTime.UtcNow;
        var row = new SalesReturn { Id=r.Id, CompanyId=r.CompanyId, ReturnNumber=r.ReturnNumber.Trim(), InvoiceId=r.InvoiceId, InvoiceNumber=r.InvoiceNumber?.Trim() ?? "", CustomerId=r.CustomerId, CustomerName=r.CustomerName?.Trim() ?? "", ReturnDate=r.ReturnDate, ItemsJson=string.IsNullOrWhiteSpace(r.ItemsJson)?"[]":r.ItemsJson, Reason=r.Reason.Trim(), Notes=r.Notes?.Trim() ?? "", RefundAmount=r.RefundAmount, Status=r.Status, CreatedAt=now, UpdatedAt=now, SyncVersion=Math.Max(1,r.SyncVersion), SyncStatus=RecordSyncStatus.Synced, LastSyncedAt=now };
        db.SalesReturns.Add(row); await db.SaveChangesAsync(ct); return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpPut("sales/{id:guid}")]
    public async Task<IActionResult> UpdateSales(Guid id, [FromBody] SalesReturnRequest r, CancellationToken ct)
    {
        var row = await db.SalesReturns.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct); if (row is null) return NotFound(new { success=false,error="Sales return not found." });
        if (!await CanAccessCompany(row.CompanyId, ct)) return Forbid();
        var error = await ValidateSales(r, ct); if (error is not null) return BadRequest(new { success=false,error });
        if (r.SyncVersion > 0 && r.SyncVersion < row.SyncVersion) return Conflict(new { success=false,error="Sales return has newer server changes." });
        row.ApplyUpdate(new SalesReturn { InvoiceId=r.InvoiceId,InvoiceNumber=r.InvoiceNumber??"",CustomerId=r.CustomerId,CustomerName=r.CustomerName??"",ReturnDate=r.ReturnDate,ItemsJson=r.ItemsJson??"[]",Reason=r.Reason,Notes=r.Notes??"",RefundAmount=r.RefundAmount,Status=r.Status });
        row.SyncStatus=RecordSyncStatus.Synced; row.LastSyncedAt=DateTime.UtcNow; await db.SaveChangesAsync(ct); return Ok(new { success=true,data=ToResponse(row) });
    }

    [HttpDelete("sales/{id:guid}")]
    public async Task<IActionResult> DeleteSales(Guid id, CancellationToken ct)
    {
        var row=await db.SalesReturns.FirstOrDefaultAsync(x=>x.Id==id&&!x.IsDeleted,ct); if(row is null)return Ok(new{success=true}); if(!await CanAccessCompany(row.CompanyId,ct))return Forbid(); row.IsDeleted=true; row.MarkPendingChange(); row.SyncStatus=RecordSyncStatus.Synced; row.LastSyncedAt=DateTime.UtcNow; await db.SaveChangesAsync(ct); return Ok(new{success=true});
    }

    [HttpPost("purchase")]
    public async Task<IActionResult> CreatePurchase([FromBody] PurchaseReturnRequest r, CancellationToken ct)
    {
        if (!await CanAccessCompany(r.CompanyId, ct)) return Forbid();
        var error = await ValidatePurchase(r, ct); if (error is not null) return BadRequest(new { success=false,error });
        var existing=await db.PurchaseReturns.FirstOrDefaultAsync(x=>x.Id==r.Id,ct); if(existing is not null)return Ok(new{success=true,data=ToResponse(existing),idempotent=true});
        if(await db.PurchaseReturns.AnyAsync(x=>x.CompanyId==r.CompanyId&&x.ReturnNumber==r.ReturnNumber&&!x.IsDeleted,ct))return Conflict(new{success=false,error="Purchase return number already exists."});
        var now=DateTime.UtcNow;
        var row=new PurchaseReturn{Id=r.Id,CompanyId=r.CompanyId,ReturnNumber=r.ReturnNumber.Trim(),PurchaseId=r.PurchaseId,PurchaseNumber=r.PurchaseNumber?.Trim()??"",SupplierId=r.SupplierId,SupplierName=r.SupplierName?.Trim()??"",ReturnDate=r.ReturnDate,ItemsJson=string.IsNullOrWhiteSpace(r.ItemsJson)?"[]":r.ItemsJson,Reason=r.Reason.Trim(),Notes=r.Notes?.Trim()??"",RefundAmount=r.RefundAmount,Status=r.Status,CreatedAt=now,UpdatedAt=now,SyncVersion=Math.Max(1,r.SyncVersion),SyncStatus=RecordSyncStatus.Synced,LastSyncedAt=now};
        db.PurchaseReturns.Add(row); await db.SaveChangesAsync(ct); return Ok(new{success=true,data=ToResponse(row)});
    }

    [HttpPut("purchase/{id:guid}")]
    public async Task<IActionResult> UpdatePurchase(Guid id,[FromBody] PurchaseReturnRequest r,CancellationToken ct)
    {
        var row=await db.PurchaseReturns.FirstOrDefaultAsync(x=>x.Id==id&&!x.IsDeleted,ct); if(row is null)return NotFound(new{success=false,error="Purchase return not found."}); if(!await CanAccessCompany(row.CompanyId,ct))return Forbid();
        var error=await ValidatePurchase(r,ct); if(error is not null)return BadRequest(new{success=false,error}); if(r.SyncVersion>0&&r.SyncVersion<row.SyncVersion)return Conflict(new{success=false,error="Purchase return has newer server changes."});
        row.ApplyUpdate(new PurchaseReturn{PurchaseId=r.PurchaseId,PurchaseNumber=r.PurchaseNumber??"",SupplierId=r.SupplierId,SupplierName=r.SupplierName??"",ReturnDate=r.ReturnDate,ItemsJson=r.ItemsJson??"[]",Reason=r.Reason,Notes=r.Notes??"",RefundAmount=r.RefundAmount,Status=r.Status}); row.SyncStatus=RecordSyncStatus.Synced; row.LastSyncedAt=DateTime.UtcNow; await db.SaveChangesAsync(ct); return Ok(new{success=true,data=ToResponse(row)});
    }

    [HttpDelete("purchase/{id:guid}")]
    public async Task<IActionResult> DeletePurchase(Guid id,CancellationToken ct)
    {
        var row=await db.PurchaseReturns.FirstOrDefaultAsync(x=>x.Id==id&&!x.IsDeleted,ct); if(row is null)return Ok(new{success=true}); if(!await CanAccessCompany(row.CompanyId,ct))return Forbid(); row.IsDeleted=true; row.MarkPendingChange(); row.SyncStatus=RecordSyncStatus.Synced; row.LastSyncedAt=DateTime.UtcNow; await db.SaveChangesAsync(ct); return Ok(new{success=true});
    }

    private async Task<string?> ValidateSales(SalesReturnRequest r,CancellationToken ct)
    {
        if(r.Id==Guid.Empty||r.CompanyId==Guid.Empty||r.InvoiceId==Guid.Empty||string.IsNullOrWhiteSpace(r.ReturnNumber)||string.IsNullOrWhiteSpace(r.Reason)||r.RefundAmount<=0)return "Return id, company, invoice, number, reason and positive refund are required.";
        return await db.Invoices.AnyAsync(x=>x.Id==r.InvoiceId&&x.CompanyId==r.CompanyId&&!x.IsDeleted,ct)?null:"Invoice not found for this company.";
    }
    private async Task<string?> ValidatePurchase(PurchaseReturnRequest r,CancellationToken ct)
    {
        if(r.Id==Guid.Empty||r.CompanyId==Guid.Empty||r.PurchaseId==Guid.Empty||string.IsNullOrWhiteSpace(r.ReturnNumber)||string.IsNullOrWhiteSpace(r.Reason)||r.RefundAmount<=0)return "Return id, company, purchase, number, reason and positive refund are required.";
        return await db.Purchases.AnyAsync(x=>x.Id==r.PurchaseId&&x.CompanyId==r.CompanyId&&!x.IsDeleted,ct)?null:"Purchase not found for this company.";
    }
    private async Task<bool> CanAccessCompany(Guid companyId,CancellationToken ct){var raw=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");return Guid.TryParse(raw,out var userId)&&await db.UserCompanies.AnyAsync(x=>x.UserId==userId&&x.CompanyId==companyId&&x.IsActive&&!x.IsDeleted,ct);}
    private static object ToResponse(BaseEntity x)=>new{id=x.Id,x.SyncVersion,x.UpdatedAt};

    public sealed class SalesReturnRequest{public Guid Id{get;set;}public Guid CompanyId{get;set;}public string ReturnNumber{get;set;}="";public Guid InvoiceId{get;set;}public string? InvoiceNumber{get;set;}public Guid CustomerId{get;set;}public string? CustomerName{get;set;}public DateTime ReturnDate{get;set;}public string? ItemsJson{get;set;}public string Reason{get;set;}="";public string? Notes{get;set;}public decimal RefundAmount{get;set;}public int Status{get;set;}public long SyncVersion{get;set;}}
    public sealed class PurchaseReturnRequest{public Guid Id{get;set;}public Guid CompanyId{get;set;}public string ReturnNumber{get;set;}="";public Guid PurchaseId{get;set;}public string? PurchaseNumber{get;set;}public Guid SupplierId{get;set;}public string? SupplierName{get;set;}public DateTime ReturnDate{get;set;}public string? ItemsJson{get;set;}public string Reason{get;set;}="";public string? Notes{get;set;}public decimal RefundAmount{get;set;}public int Status{get;set;}public long SyncVersion{get;set;}}
}
