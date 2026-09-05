using System.Security.Claims;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.API.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
public sealed class CustomersController(AppDbContext db) : ControllerBase
{
    [HttpGet("company/{companyId:guid}")]
    public async Task<IActionResult> GetAll(Guid companyId, CancellationToken ct)
    {
        if (!await CanAccessCompany(companyId, ct)) return Forbid();
        var rows = await db.Customers.AsNoTracking()
            .Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
        return Ok(new { success = true, data = rows });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomerSyncRequest r, CancellationToken ct)
    {
        if (!await CanAccessCompany(r.CompanyId, ct)) return Forbid();
        if (r.Id == Guid.Empty || string.IsNullOrWhiteSpace(r.Name))
            return BadRequest(new { success = false, error = "Customer id and name are required." });

        var existing = await db.Customers.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (existing is not null)
            return Ok(new { success = true, data = ToResponse(existing), idempotent = true });

        var now = DateTime.UtcNow;
        var row = new Customer
        {
            Id = r.Id,
            CompanyId = r.CompanyId,
            Name = r.Name.Trim(),
            Email = r.Email?.Trim() ?? string.Empty,
            Phone = r.Phone?.Trim() ?? string.Empty,
            Address = r.Address?.Trim() ?? string.Empty,
            City = r.City?.Trim() ?? string.Empty,
            Country = r.Country?.Trim() ?? string.Empty,
            TaxNumber = r.TaxNumber?.Trim() ?? string.Empty,
            Notes = r.Notes?.Trim() ?? string.Empty,
            Status = r.Status,
            OutstandingBalance = r.OutstandingBalance,
            TotalPurchases = r.TotalPurchases,
            CreatedAt = now,
            UpdatedAt = now,
            SyncVersion = Math.Max(1, r.SyncVersion),
            SyncStatus = RecordSyncStatus.Synced,
            LastSyncedAt = now
        };
        db.Customers.Add(row);
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CustomerSyncRequest r, CancellationToken ct)
    {
        var row = await db.Customers.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (row is null) return NotFound(new { success = false, error = "Customer not found." });
        if (!await CanAccessCompany(row.CompanyId, ct)) return Forbid();
        if (r.SyncVersion > 0 && r.SyncVersion < row.SyncVersion)
            return Conflict(new { success = false, error = "Customer has newer server changes." });

        row.ApplyUpdate(r.Name, r.Email, r.Phone, r.Address, r.City, r.Country, r.TaxNumber, r.Notes,
            r.Status, r.OutstandingBalance, r.TotalPurchases);
        row.SyncStatus = RecordSyncStatus.Synced;
        row.LastSyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var row = await db.Customers.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
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

    private static object ToResponse(Customer x) => new { id = x.Id, x.SyncVersion, x.UpdatedAt };

    public sealed class CustomerSyncRequest
    {
        public Guid Id { get; set; }
        public Guid CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? TaxNumber { get; set; }
        public string? Notes { get; set; }
        public int Status { get; set; }
        public decimal OutstandingBalance { get; set; }
        public decimal TotalPurchases { get; set; }
        public long SyncVersion { get; set; }
    }
}
