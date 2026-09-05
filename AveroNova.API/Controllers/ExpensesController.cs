using System.Security.Claims;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.API.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize]
public sealed class ExpensesController(AppDbContext db) : ControllerBase
{
    private const int ExpenseStatusPending = 0;
    private const int ExpenseStatusApproved = 1;
    private const int ExpenseStatusRejected = 2;
    private const int ExpenseStatusPaid = 3;
    private const int PaymentMethodCash = 0;
    private const int PaymentMethodOther = 6;

    [HttpGet("company/{companyId:guid}")]
    public async Task<IActionResult> GetAll(Guid companyId, CancellationToken ct)
    {
        if (!await CanAccessCompany(companyId, ct)) return Forbid();
        var rows = await db.Expenses.AsNoTracking().Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .OrderByDescending(x => x.ExpenseDate).ThenByDescending(x => x.CreatedAt).ToListAsync(ct);
        return Ok(new { success = true, data = rows });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ExpenseSyncRequest r, CancellationToken ct)
    {
        if (!await CanAccessCompany(r.CompanyId, ct)) return Forbid();
        var validation = Validate(r); if (validation is not null) return BadRequest(new { success = false, error = validation });
        var existing = await db.Expenses.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (existing is not null) return Ok(new { success = true, data = ToResponse(existing), idempotent = true });
        var now = DateTime.UtcNow;
        var row = new Expense { Id=r.Id, CompanyId=r.CompanyId, Category=r.Category.Trim(), Description=r.Description?.Trim()??string.Empty,
            Amount=r.Amount, ExpenseDate=r.ExpenseDate, Method=r.Method, Reference=r.Reference?.Trim()??string.Empty,
            Notes=r.Notes?.Trim()??string.Empty, Status=r.Status, ApprovedBy=r.ApprovedBy?.Trim()??string.Empty,
            CreatedAt=now, UpdatedAt=now, SyncVersion=Math.Max(1,r.SyncVersion), SyncStatus=RecordSyncStatus.Synced, LastSyncedAt=now };
        db.Expenses.Add(row); await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ExpenseSyncRequest r, CancellationToken ct)
    {
        var row=await db.Expenses.FirstOrDefaultAsync(x=>x.Id==id&&!x.IsDeleted,ct);
        if(row is null)return NotFound(new{success=false,error="Expense not found."});
        if(!await CanAccessCompany(row.CompanyId,ct))return Forbid();
        if(r.CompanyId!=Guid.Empty&&r.CompanyId!=row.CompanyId)return BadRequest(new{success=false,error="Expense company cannot be changed."});
        if(r.SyncVersion>0&&r.SyncVersion<row.SyncVersion)return Conflict(new{success=false,error="Expense has newer server changes."});
        var validation=Validate(r);if(validation is not null)return BadRequest(new{success=false,error=validation});
        row.ApplyUpdate(r.Category,r.Description,r.Amount,r.ExpenseDate,r.Method,r.Reference,r.Notes,r.Status,r.ApprovedBy);
        row.SyncStatus=RecordSyncStatus.Synced;row.LastSyncedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);
        return Ok(new{success=true,data=ToResponse(row)});
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id,CancellationToken ct)
    {
        var row=await db.Expenses.FirstOrDefaultAsync(x=>x.Id==id&&!x.IsDeleted,ct);
        if(row is null)return Ok(new{success=true});
        if(!await CanAccessCompany(row.CompanyId,ct))return Forbid();
        row.IsDeleted=true;row.MarkPendingChange();row.SyncStatus=RecordSyncStatus.Synced;row.LastSyncedAt=DateTime.UtcNow;
        await db.SaveChangesAsync(ct);return Ok(new{success=true});
    }

    private static string? Validate(ExpenseSyncRequest r)
    {
        if(r.Id==Guid.Empty||r.CompanyId==Guid.Empty)return "Expense id and company are required.";
        if(string.IsNullOrWhiteSpace(r.Category))return "Expense category is required.";
        if(r.Category.Trim().Length>100)return "Category must be 100 characters or fewer.";
        if(r.Amount<=0)return "Expense amount must be greater than zero.";
        if(r.ExpenseDate.Date>DateTime.UtcNow.Date)return "Expense date cannot be in the future.";
        if(r.Method < PaymentMethodCash || r.Method > PaymentMethodOther)return "Expense payment method is invalid.";
        if(r.Status < ExpenseStatusPending || r.Status > ExpenseStatusPaid)return "Expense status is invalid.";
        if(r.Status is ExpenseStatusApproved or ExpenseStatusPaid && string.IsNullOrWhiteSpace(r.ApprovedBy))return "Approved by is required for approved or paid expenses.";
        return null;
    }

    private async Task<bool> CanAccessCompany(Guid companyId,CancellationToken ct)
    { var raw=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"); if(!Guid.TryParse(raw,out var userId))return false;
      return await db.UserCompanies.AnyAsync(x=>x.UserId==userId&&x.CompanyId==companyId&&x.IsActive&&!x.IsDeleted,ct); }
    private static object ToResponse(Expense x)=>new{id=x.Id,x.SyncVersion,x.UpdatedAt};

    public sealed class ExpenseSyncRequest
    { public Guid Id{get;set;} public Guid CompanyId{get;set;} public string Category{get;set;}=string.Empty; public string? Description{get;set;}
      public decimal Amount{get;set;} public DateTime ExpenseDate{get;set;} public int Method{get;set;} public string? Reference{get;set;}
      public string? Notes{get;set;} public int Status{get;set;} public string? ApprovedBy{get;set;} public long SyncVersion{get;set;} }
}
