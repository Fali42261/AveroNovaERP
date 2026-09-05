using System.Security.Claims;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.API.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize]
public sealed class SuppliersController(AppDbContext db) : ControllerBase
{
    [HttpGet("company/{companyId:guid}")]
    public async Task<IActionResult> GetAll(Guid companyId, CancellationToken ct)
    {
        if (!await CanAccessCompany(companyId, ct)) return Forbid();
        var rows = await db.Suppliers.AsNoTracking()
            .Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .OrderBy(x => x.Name).ToListAsync(ct);
        return Ok(new { success = true, data = rows });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SupplierSyncRequest r, CancellationToken ct)
    {
        if (!await CanAccessCompany(r.CompanyId, ct)) return Forbid();
        if (r.Id == Guid.Empty || r.CompanyId == Guid.Empty || string.IsNullOrWhiteSpace(r.Name))
            return BadRequest(new { success = false, error = "Supplier id, company and name are required." });

        var existing = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (existing is not null) return Ok(new { success = true, data = ToResponse(existing), idempotent = true });

        var normalized = r.Name.Trim();
        if (await db.Suppliers.AnyAsync(x => x.CompanyId == r.CompanyId && !x.IsDeleted && x.Name.ToLower() == normalized.ToLower(), ct))
            return Conflict(new { success = false, error = "A supplier with this name already exists." });

        var now = DateTime.UtcNow;
        var row = new Supplier
        {
            Id = r.Id,
            CompanyId = r.CompanyId,
            Name = normalized,
            Email = r.Email?.Trim() ?? string.Empty,
            Phone = r.Phone?.Trim() ?? string.Empty,
            Address = r.Address?.Trim() ?? string.Empty,
            TaxNumber = r.TaxNumber?.Trim() ?? string.Empty,
            Notes = r.Notes?.Trim() ?? string.Empty,
            IsActive = r.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
            SyncVersion = Math.Max(1, r.SyncVersion),
            SyncStatus = RecordSyncStatus.Synced,
            LastSyncedAt = now
        };
        db.Suppliers.Add(row);
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SupplierSyncRequest r, CancellationToken ct)
    {
        var row = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (row is null) return NotFound(new { success = false, error = "Supplier not found." });
        if (!await CanAccessCompany(row.CompanyId, ct)) return Forbid();
        if (r.CompanyId != Guid.Empty && r.CompanyId != row.CompanyId)
            return BadRequest(new { success = false, error = "Supplier company cannot be changed." });
        if (r.SyncVersion > 0 && r.SyncVersion < row.SyncVersion)
            return Conflict(new { success = false, error = "Supplier has newer server changes." });
        if (string.IsNullOrWhiteSpace(r.Name)) return BadRequest(new { success = false, error = "Supplier name is required." });

        var normalized = r.Name.Trim();
        if (await db.Suppliers.AnyAsync(x => x.CompanyId == row.CompanyId && x.Id != row.Id && !x.IsDeleted && x.Name.ToLower() == normalized.ToLower(), ct))
            return Conflict(new { success = false, error = "A supplier with this name already exists." });

        row.ApplyUpdate(normalized, r.Email, r.Phone, r.Address, r.TaxNumber, r.Notes, r.IsActive);
        row.SyncStatus = RecordSyncStatus.Synced;
        row.LastSyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var row = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (row is null) return Ok(new { success = true });
        if (!await CanAccessCompany(row.CompanyId, ct)) return Forbid();
        if (await db.Purchases.AnyAsync(x => x.CompanyId == row.CompanyId && x.SupplierId == id && !x.IsDeleted, ct))
            return Conflict(new { success = false, error = "Supplier cannot be deleted because purchases reference it." });
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

    private static object ToResponse(Supplier x) => new { id = x.Id, x.SyncVersion, x.UpdatedAt };

    public sealed class SupplierSyncRequest
    {
        public Guid Id { get; set; }
        public Guid CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? TaxNumber { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        public long SyncVersion { get; set; }
    }
}
