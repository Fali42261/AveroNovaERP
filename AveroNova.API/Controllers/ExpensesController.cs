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
    [HttpGet("company/{companyId:guid}")]
    public async Task<IActionResult> GetAll(Guid companyId, CancellationToken ct)
    {
        if (!await CanAccessCompany(companyId, ct)) return Forbid();
        var rows = await db.Expenses.AsNoTracking()
            .Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .OrderByDescending(x => x.ExpenseDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return Ok(new { success = true, data = rows });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ExpenseSyncRequest r, CancellationToken ct)
    {
        if (!await CanAccessCompany(r.CompanyId, ct)) return Forbid();
        var validation = Validate(r);
        if (validation is not null) return BadRequest(new { success = false, error = validation });

        var existing = await db.Expenses.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (existing is not null)
            return Ok(new { success = true, data = ToResponse(existing), idempotent = true });

        if (await db.Expenses.AnyAsync(x => x.CompanyId == r.CompanyId && x.ExpenseNumber == r.ExpenseNumber && !x.IsDeleted, ct))
            return Conflict(new { success = false, error = "Expense number already exists." });

        var now = DateTime.UtcNow;
        var row = new Expense
        {
            Id = r.Id,
            CompanyId = r.CompanyId,
            ExpenseNumber = r.ExpenseNumber.Trim(),
            ExpenseDate = r.ExpenseDate,
            Category = r.Category.Trim(),
            Payee = r.Payee?.Trim() ?? string.Empty,
            Amount = r.Amount,
            PaymentMethod = r.PaymentMethod,
            Reference = r.Reference?.Trim() ?? string.Empty,
            Notes = r.Notes?.Trim() ?? string.Empty,
            CreatedAt = now,
            UpdatedAt = now,
            SyncVersion = Math.Max(1, r.SyncVersion),
            SyncStatus = RecordSyncStatus.Synced,
            LastSyncedAt = now
        };
        db.Expenses.Add(row);
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ExpenseSyncRequest r, CancellationToken ct)
    {
        var row = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (row is null) return NotFound(new { success = false, error = "Expense not found." });
        if (!await CanAccessCompany(row.CompanyId, ct)) return Forbid();
        if (r.CompanyId != Guid.Empty && r.CompanyId != row.CompanyId)
            return BadRequest(new { success = false, error = "Expense company cannot be changed." });
        if (r.SyncVersion > 0 && r.SyncVersion < row.SyncVersion)
            return Conflict(new { success = false, error = "Expense has newer server changes." });

        var validation = Validate(r);
        if (validation is not null) return BadRequest(new { success = false, error = validation });

        if (await db.Expenses.AnyAsync(x => x.Id != id && x.CompanyId == row.CompanyId && x.ExpenseNumber == r.ExpenseNumber && !x.IsDeleted, ct))
            return Conflict(new { success = false, error = "Expense number already exists." });

        row.ApplyUpdate(r.ExpenseNumber, r.ExpenseDate, r.Category, r.Payee ?? string.Empty,
            r.Amount, r.PaymentMethod, r.Reference, r.Notes);
        row.SyncStatus = RecordSyncStatus.Synced;
        row.LastSyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var row = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (row is null) return Ok(new { success = true });
        if (!await CanAccessCompany(row.CompanyId, ct)) return Forbid();

        row.IsDeleted = true;
        row.MarkPendingChange();
        row.SyncStatus = RecordSyncStatus.Synced;
        row.LastSyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    private static string? Validate(ExpenseSyncRequest r)
    {
        if (r.Id == Guid.Empty || r.CompanyId == Guid.Empty || string.IsNullOrWhiteSpace(r.ExpenseNumber))
            return "Expense id, company and expense number are required.";
        if (string.IsNullOrWhiteSpace(r.Category)) return "Expense category is required.";
        if (r.Amount <= 0) return "Expense amount must be greater than zero.";
        if (r.PaymentMethod < 0) return "Payment method is invalid.";
        return null;
    }

    private async Task<bool> CanAccessCompany(Guid companyId, CancellationToken ct)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var userId)) return false;
        return await db.UserCompanies.AnyAsync(x => x.UserId == userId && x.CompanyId == companyId && x.IsActive && !x.IsDeleted, ct);
    }

    private static object ToResponse(Expense x) => new { id = x.Id, x.SyncVersion, x.UpdatedAt };

    public sealed class ExpenseSyncRequest
    {
        public Guid Id { get; set; }
        public Guid CompanyId { get; set; }
        public string ExpenseNumber { get; set; } = string.Empty;
        public DateTime ExpenseDate { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? Payee { get; set; }
        public decimal Amount { get; set; }
        public int PaymentMethod { get; set; }
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public long SyncVersion { get; set; }
    }
}
