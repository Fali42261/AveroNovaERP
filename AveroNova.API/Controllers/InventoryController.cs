using System.Security.Claims;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.API.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public sealed class InventoryController(AppDbContext db) : ControllerBase
{
    [HttpGet("movements/company/{companyId:guid}")]
    public async Task<IActionResult> GetMovements(Guid companyId, CancellationToken ct)
    {
        if (!await CanAccessCompany(companyId, ct)) return Forbid();

        var rows = await db.StockMovements.AsNoTracking()
            .Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return Ok(new { success = true, data = rows });
    }

    [HttpPost("movements")]
    public async Task<IActionResult> CreateMovement([FromBody] StockMovementSyncRequest r, CancellationToken ct)
    {
        if (r.Id == Guid.Empty || r.CompanyId == Guid.Empty || r.ProductId == Guid.Empty)
            return BadRequest(new { success = false, error = "Movement, company and product ids are required." });
        if (!await CanAccessCompany(r.CompanyId, ct)) return Forbid();
        if (r.StockAfter < 0)
            return BadRequest(new { success = false, error = "Stock cannot be negative." });

        var existing = await db.StockMovements.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (existing is not null)
            return Ok(new { success = true, data = ToResponse(existing), idempotent = true });

        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == r.ProductId && x.CompanyId == r.CompanyId && !x.IsDeleted, ct);
        if (product is null)
            return NotFound(new { success = false, error = "Product not found." });

        var now = DateTime.UtcNow;
        product.Stock = r.StockAfter;
        product.UpdatedAt = now;
        product.SyncVersion = Math.Max(1, product.SyncVersion + 1);
        product.SyncStatus = RecordSyncStatus.Synced;
        product.LastSyncedAt = now;

        var movement = new StockMovement
        {
            Id = r.Id,
            CompanyId = r.CompanyId,
            ProductId = r.ProductId,
            ProductName = string.IsNullOrWhiteSpace(r.ProductName) ? product.Name : r.ProductName.Trim(),
            SKU = string.IsNullOrWhiteSpace(r.Sku) ? product.SKU : r.Sku.Trim(),
            Type = r.Type,
            Quantity = r.Quantity,
            StockBefore = r.StockBefore,
            StockAfter = r.StockAfter,
            Reference = r.Reference?.Trim() ?? string.Empty,
            Notes = r.Notes?.Trim() ?? string.Empty,
            CreatedBy = r.CreatedBy?.Trim() ?? string.Empty,
            CreatedAt = now,
            UpdatedAt = now,
            SyncVersion = Math.Max(1, r.SyncVersion),
            SyncStatus = RecordSyncStatus.Synced,
            LastSyncedAt = now
        };

        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = ToResponse(movement) });
    }

    private async Task<bool> CanAccessCompany(Guid companyId, CancellationToken ct)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var userId)) return false;
        return await db.UserCompanies.AnyAsync(x => x.UserId == userId && x.CompanyId == companyId && x.IsActive && !x.IsDeleted, ct);
    }

    private static object ToResponse(StockMovement x) => new { id = x.Id, x.SyncVersion, x.UpdatedAt };

    public sealed class StockMovementSyncRequest
    {
        public Guid Id { get; set; }
        public Guid CompanyId { get; set; }
        public Guid ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? Sku { get; set; }
        public int Type { get; set; }
        public int Quantity { get; set; }
        public int StockBefore { get; set; }
        public int StockAfter { get; set; }
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public string? CreatedBy { get; set; }
        public long SyncVersion { get; set; }
    }
}
