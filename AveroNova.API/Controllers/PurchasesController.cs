using System.Security.Claims;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.API.Controllers;

[ApiController]
[Route("api/purchases")]
[Authorize]
public sealed class PurchasesController(AppDbContext db) : ControllerBase
{
    [HttpGet("company/{companyId:guid}")]
    public async Task<IActionResult> GetAll(Guid companyId, CancellationToken ct)
    {
        if (!await CanAccessCompany(companyId, ct)) return Forbid();
        var rows = await db.Purchases.AsNoTracking()
            .Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .OrderByDescending(x => x.PurchaseDate).ToListAsync(ct);
        return Ok(new { success = true, data = rows });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PurchaseSyncRequest r, CancellationToken ct)
    {
        if (!await CanAccessCompany(r.CompanyId, ct)) return Forbid();
        var validation = await Validate(r, ct);
        if (validation is not null) return BadRequest(new { success = false, error = validation });

        var existing = await db.Purchases.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (existing is not null) return Ok(new { success = true, data = ToResponse(existing), idempotent = true });

        if (await db.Purchases.AnyAsync(x => x.CompanyId == r.CompanyId && x.PurchaseNumber == r.PurchaseNumber && !x.IsDeleted, ct))
            return Conflict(new { success = false, error = "Purchase number already exists." });

        var now = DateTime.UtcNow;
        var row = new Purchase
        {
            Id = r.Id,
            CompanyId = r.CompanyId,
            PurchaseNumber = r.PurchaseNumber.Trim(),
            SupplierId = r.SupplierId,
            SupplierName = r.SupplierName?.Trim() ?? string.Empty,
            PurchaseDate = r.PurchaseDate,
            DueDate = r.DueDate,
            ItemsJson = string.IsNullOrWhiteSpace(r.ItemsJson) ? "[]" : r.ItemsJson,
            PaymentMethod = r.PaymentMethod,
            Reference = r.Reference?.Trim() ?? string.Empty,
            Notes = r.Notes?.Trim() ?? string.Empty,
            Status = r.Status,
            PaidAmount = r.PaidAmount,
            CreatedAt = now,
            UpdatedAt = now,
            SyncVersion = Math.Max(1, r.SyncVersion),
            SyncStatus = RecordSyncStatus.Synced,
            LastSyncedAt = now
        };
        db.Purchases.Add(row);
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PurchaseSyncRequest r, CancellationToken ct)
    {
        var row = await db.Purchases.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (row is null) return NotFound(new { success = false, error = "Purchase not found." });
        if (!await CanAccessCompany(row.CompanyId, ct)) return Forbid();
        if (r.CompanyId != Guid.Empty && r.CompanyId != row.CompanyId)
            return BadRequest(new { success = false, error = "Purchase company cannot be changed." });
        if (r.SyncVersion > 0 && r.SyncVersion < row.SyncVersion)
            return Conflict(new { success = false, error = "Purchase has newer server changes." });

        var validation = await Validate(r with { CompanyId = row.CompanyId, Id = id }, ct);
        if (validation is not null) return BadRequest(new { success = false, error = validation });

        if (await db.Purchases.AnyAsync(x => x.CompanyId == row.CompanyId && x.Id != row.Id && x.PurchaseNumber == r.PurchaseNumber && !x.IsDeleted, ct))
            return Conflict(new { success = false, error = "Purchase number already exists." });

        row.ApplyUpdate(r.PurchaseNumber, r.SupplierId, r.SupplierName ?? string.Empty, r.PurchaseDate, r.DueDate,
            r.ItemsJson ?? "[]", r.PaymentMethod, r.Reference, r.Notes, r.Status, r.PaidAmount);
        row.SyncStatus = RecordSyncStatus.Synced;
        row.LastSyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var row = await db.Purchases.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (row is null) return Ok(new { success = true });
        if (!await CanAccessCompany(row.CompanyId, ct)) return Forbid();
        row.IsDeleted = true;
        row.MarkPendingChange();
        row.SyncStatus = RecordSyncStatus.Synced;
        row.LastSyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    private async Task<string?> Validate(PurchaseSyncRequest r, CancellationToken ct)
    {
        if (r.Id == Guid.Empty || r.CompanyId == Guid.Empty || r.SupplierId == Guid.Empty || string.IsNullOrWhiteSpace(r.PurchaseNumber))
            return "Purchase id, company, supplier and purchase number are required.";
        if (r.DueDate.Date < r.PurchaseDate.Date) return "Due date cannot be before purchase date.";
        if (r.PaidAmount < 0) return "Paid amount cannot be negative.";
        if (!await db.Suppliers.AnyAsync(x => x.Id == r.SupplierId && x.CompanyId == r.CompanyId && x.IsActive && !x.IsDeleted, ct))
            return "Supplier not found for this company.";
        return null;
    }

    private async Task<bool> CanAccessCompany(Guid companyId, CancellationToken ct)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var userId)) return false;
        return await db.UserCompanies.AnyAsync(x => x.UserId == userId && x.CompanyId == companyId && x.IsActive && !x.IsDeleted, ct);
    }

    private static object ToResponse(Purchase x) => new { id = x.Id, x.SyncVersion, x.UpdatedAt };

    public sealed record PurchaseSyncRequest
    {
        public Guid Id { get; init; }
        public Guid CompanyId { get; init; }
        public string PurchaseNumber { get; init; } = string.Empty;
        public Guid SupplierId { get; init; }
        public string? SupplierName { get; init; }
        public DateTime PurchaseDate { get; init; }
        public DateTime DueDate { get; init; }
        public string? ItemsJson { get; init; }
        public int PaymentMethod { get; init; }
        public string? Reference { get; init; }
        public string? Notes { get; init; }
        public int Status { get; init; }
        public decimal PaidAmount { get; init; }
        public long SyncVersion { get; init; }
    }
}
