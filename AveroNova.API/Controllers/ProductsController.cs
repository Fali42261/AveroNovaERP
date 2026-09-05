using System.Security.Claims;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.API.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public sealed class ProductsController(AppDbContext db) : ControllerBase
{
    [HttpGet("company/{companyId:guid}")]
    public async Task<IActionResult> GetAll(Guid companyId, CancellationToken ct)
    {
        if (!await CanAccessCompany(companyId, ct)) return Forbid();
        var rows = await db.Products.AsNoTracking()
            .Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
        return Ok(new { success = true, data = rows });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductSyncRequest r, CancellationToken ct)
    {
        if (!await CanAccessCompany(r.CompanyId, ct)) return Forbid();
        if (r.Id == Guid.Empty || string.IsNullOrWhiteSpace(r.Name))
            return BadRequest(new { success = false, error = "Product id and name are required." });

        var existing = await db.Products.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (existing is not null)
            return Ok(new { success = true, data = ToResponse(existing), idempotent = true });

        var now = DateTime.UtcNow;
        var row = new Product
        {
            Id = r.Id,
            CompanyId = r.CompanyId,
            Name = r.Name.Trim(),
            SKU = r.Sku?.Trim() ?? string.Empty,
            Barcode = r.Barcode?.Trim() ?? string.Empty,
            Category = r.Category?.Trim() ?? string.Empty,
            Brand = r.Brand?.Trim() ?? string.Empty,
            Unit = string.IsNullOrWhiteSpace(r.Unit) ? "pcs" : r.Unit.Trim(),
            PurchasePrice = r.PurchasePrice,
            SellingPrice = r.SellingPrice,
            TaxPercent = r.TaxPercent,
            Stock = r.Stock,
            MinimumStock = r.MinimumStock,
            Description = r.Description?.Trim() ?? string.Empty,
            Status = r.Status,
            CreatedAt = now,
            UpdatedAt = now,
            SyncVersion = Math.Max(1, r.SyncVersion),
            SyncStatus = RecordSyncStatus.Synced,
            LastSyncedAt = now
        };
        db.Products.Add(row);
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ProductSyncRequest r, CancellationToken ct)
    {
        var row = await db.Products.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (row is null) return NotFound(new { success = false, error = "Product not found." });
        if (!await CanAccessCompany(row.CompanyId, ct)) return Forbid();
        if (r.SyncVersion > 0 && r.SyncVersion < row.SyncVersion)
            return Conflict(new { success = false, error = "Product has newer server changes." });

        row.ApplyUpdate(r.Name, r.Sku, r.Barcode, r.Category, r.Brand, r.Unit, r.PurchasePrice,
            r.SellingPrice, r.TaxPercent, r.Stock, r.MinimumStock, r.Description, r.Status);
        row.SyncStatus = RecordSyncStatus.Synced;
        row.LastSyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var row = await db.Products.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
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

    private static object ToResponse(Product x) => new { id = x.Id, x.SyncVersion, x.UpdatedAt };

    public sealed class ProductSyncRequest
    {
        public Guid Id { get; set; }
        public Guid CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public string? Barcode { get; set; }
        public string? Category { get; set; }
        public string? Brand { get; set; }
        public string? Unit { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal TaxPercent { get; set; }
        public int Stock { get; set; }
        public int MinimumStock { get; set; }
        public string? Description { get; set; }
        public int Status { get; set; }
        public long SyncVersion { get; set; }
    }
}
