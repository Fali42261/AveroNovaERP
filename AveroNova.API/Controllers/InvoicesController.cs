using System.Security.Claims;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.API.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize]
public sealed class InvoicesController(AppDbContext db) : ControllerBase
{
    [HttpGet("company/{companyId:guid}")]
    public async Task<IActionResult> GetAll(Guid companyId, CancellationToken ct)
    {
        if (!await CanAccessCompany(companyId, ct)) return Forbid();
        var rows = await db.Invoices.AsNoTracking()
            .Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .OrderByDescending(x => x.InvoiceDate)
            .ToListAsync(ct);
        return Ok(new { success = true, data = rows });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] InvoiceSyncRequest r, CancellationToken ct)
    {
        if (!await CanAccessCompany(r.CompanyId, ct)) return Forbid();
        if (r.Id == Guid.Empty || r.CompanyId == Guid.Empty || r.CustomerId == Guid.Empty || string.IsNullOrWhiteSpace(r.InvoiceNumber))
            return BadRequest(new { success = false, error = "Invoice id, company, customer and invoice number are required." });

        var existing = await db.Invoices.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (existing is not null)
            return Ok(new { success = true, data = ToResponse(existing), idempotent = true });

        var duplicate = await db.Invoices.AnyAsync(x => x.CompanyId == r.CompanyId && x.InvoiceNumber == r.InvoiceNumber && !x.IsDeleted, ct);
        if (duplicate)
            return Conflict(new { success = false, error = "Invoice number already exists." });

        var now = DateTime.UtcNow;
        var row = new Invoice
        {
            Id = r.Id,
            CompanyId = r.CompanyId,
            InvoiceNumber = r.InvoiceNumber.Trim(),
            CustomerId = r.CustomerId,
            CustomerName = r.CustomerName?.Trim() ?? string.Empty,
            InvoiceDate = r.InvoiceDate,
            DueDate = r.DueDate,
            ItemsJson = string.IsNullOrWhiteSpace(r.ItemsJson) ? "[]" : r.ItemsJson,
            DiscountPct = r.DiscountPct,
            TaxPct = r.TaxPct,
            PaymentMethod = r.PaymentMethod,
            Notes = r.Notes?.Trim() ?? string.Empty,
            Status = r.Status,
            PaidAmount = r.PaidAmount,
            CreatedAt = now,
            UpdatedAt = now,
            SyncVersion = Math.Max(1, r.SyncVersion),
            SyncStatus = RecordSyncStatus.Synced,
            LastSyncedAt = now
        };
        db.Invoices.Add(row);
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] InvoiceSyncRequest r, CancellationToken ct)
    {
        var row = await db.Invoices.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (row is null) return NotFound(new { success = false, error = "Invoice not found." });
        if (!await CanAccessCompany(row.CompanyId, ct)) return Forbid();
        if (r.CompanyId != Guid.Empty && r.CompanyId != row.CompanyId) return BadRequest(new { success = false, error = "Invoice company cannot be changed." });
        if (r.SyncVersion > 0 && r.SyncVersion < row.SyncVersion)
            return Conflict(new { success = false, error = "Invoice has newer server changes." });

        row.ApplyUpdate(r.InvoiceNumber, r.CustomerId, r.CustomerName ?? string.Empty, r.InvoiceDate, r.DueDate,
            r.ItemsJson ?? "[]", r.DiscountPct, r.TaxPct, r.PaymentMethod, r.Notes, r.Status, r.PaidAmount);
        row.SyncStatus = RecordSyncStatus.Synced;
        row.LastSyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var row = await db.Invoices.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (row is null) return Ok(new { success = true });
        if (!await CanAccessCompany(row.CompanyId, ct)) return Forbid();
        row.IsDeleted = true;
        row.MarkPendingChange();
        row.SyncStatus = RecordSyncStatus.Synced;
        row.LastSyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    private async Task<bool> CanAccessCompany(Guid companyId, CancellationToken ct)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var userId)) return false;
        return await db.UserCompanies.AnyAsync(x => x.UserId == userId && x.CompanyId == companyId && x.IsActive && !x.IsDeleted, ct);
    }

    private static object ToResponse(Invoice x) => new { id = x.Id, x.SyncVersion, x.UpdatedAt };

    public sealed class InvoiceSyncRequest
    {
        public Guid Id { get; set; }
        public Guid CompanyId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public string? ItemsJson { get; set; }
        public decimal DiscountPct { get; set; }
        public decimal TaxPct { get; set; }
        public int PaymentMethod { get; set; }
        public string? Notes { get; set; }
        public int Status { get; set; }
        public decimal PaidAmount { get; set; }
        public long SyncVersion { get; set; }
    }
}
