using AveroNova.Application.Interfaces.Repositories;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Local;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services.Mock;

public sealed class MockInventoryService : IInventoryService
{
    private readonly IProductRepository _products;
    private readonly IDbContextFactory<AppDbContext> _factory;

    public MockInventoryService(IProductRepository products, IDbContextFactory<AppDbContext> factory)
    {
        _products = products;
        _factory = factory;
    }

    public async Task<List<InventoryItemModel>> GetInventoryAsync(Guid companyId)
    {
        var (items, _) = await _products.QueryAsync(companyId, null, null, 0, 0);
        return items.Select(Map).OrderBy(x => x.ProductName).ToList();
    }

    public async Task<InventoryItemModel?> GetByProductIdAsync(Guid productId)
    {
        if (productId == Guid.Empty) return null;
        var product = await FindProductAsync(productId);
        return product == null ? null : Map(product);
    }

    public async Task<List<StockMovementModel>> GetMovementsAsync(Guid companyId, Guid? productId = null)
    {
        if (companyId == Guid.Empty) return [];
        await using var db = await _factory.CreateDbContextAsync();
        var query = db.StockMovements.AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.CompanyId == companyId && !x.IsDeleted);
        if (productId is Guid id && id != Guid.Empty)
            query = query.Where(x => x.ProductId == id);

        var rows = await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
        return rows.Select(x => new StockMovementModel
        {
            LocalId = x.Id,
            ProductId = x.ProductId,
            ProductName = x.Product?.Name ?? string.Empty,
            SKU = x.Product?.SKU ?? string.Empty,
            Type = Enum.IsDefined(typeof(StockMovementType), x.MovementType)
                ? (StockMovementType)x.MovementType
                : StockMovementType.Adjustment,
            Quantity = x.MovementType == (int)StockMovementType.Out ? -Math.Abs(x.Quantity) : x.Quantity,
            StockBefore = x.StockBefore,
            StockAfter = x.StockAfter,
            Reference = x.Reference,
            Notes = x.Notes,
            CreatedBy = x.CreatedBy,
            CompanyId = x.CompanyId,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt ?? x.CreatedAt,
            IsDeleted = x.IsDeleted,
            SyncStatus = Enum.IsDefined(typeof(SyncStatus), x.SyncStatus)
                ? (SyncStatus)x.SyncStatus
                : SyncStatus.PendingSync
        }).ToList();
    }

    public async Task<(bool Ok, string? Error)> AdjustStockAsync(StockAdjustmentModel adjustment)
    {
        if (adjustment.CompanyId == Guid.Empty || adjustment.ProductId == Guid.Empty)
            return (false, "Company and product are required.");
        if (adjustment.NewStock < 0)
            return (false, "Stock cannot be negative.");

        await using var db = await _factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var product = await db.Products.FirstOrDefaultAsync(x =>
                x.CompanyId == adjustment.CompanyId && x.Id == adjustment.ProductId && !x.IsDeleted);
            if (product == null) return (false, "Inventory item not found.");

            var before = product.Stock;
            var after = adjustment.NewStock;
            var difference = after - before;
            if (difference == 0) return (false, "New stock is the same as current stock.");

            var now = DateTime.UtcNow;
            product.Stock = after;
            product.UpdatedAt = now;

            db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                CompanyId = adjustment.CompanyId,
                ProductId = adjustment.ProductId,
                MovementType = (int)StockMovementType.Adjustment,
                Quantity = difference,
                StockBefore = before,
                StockAfter = after,
                Reference = "STK-ADJ-" + now.ToString("yyyyMMddHHmmss"),
                Notes = string.IsNullOrWhiteSpace(adjustment.Notes) ? adjustment.Reason : $"{adjustment.Reason} - {adjustment.Notes}",
                CreatedBy = string.IsNullOrWhiteSpace(adjustment.AdjustedBy) ? "Admin" : adjustment.AdjustedBy,
                SyncStatus = (int)SyncStatus.PendingSync,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false
            });

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Stock adjustment failed: {ex}");
            return (false, "Unable to save stock adjustment locally.");
        }
    }

    private async Task<Product?> FindProductAsync(Guid productId)
    {
        var companyId = LocalSessionStore.CompanyId ?? Guid.Empty;
        if (companyId == Guid.Empty) return null;
        return await _products.GetByIdAsync(companyId, productId);
    }

    private static InventoryItemModel Map(Product product) => new()
    {
        LocalId = product.Id,
        ProductId = product.Id,
        ProductName = product.Name,
        SKU = product.SKU,
        Category = product.Category,
        CurrentStock = product.Stock,
        AvailableStock = product.Stock,
        ReservedStock = 0,
        MinimumStock = product.MinimumStock,
        CompanyId = product.CompanyId,
        CreatedAt = product.CreatedAt,
        UpdatedAt = product.UpdatedAt ?? product.CreatedAt,
        LastUpdated = product.UpdatedAt ?? product.CreatedAt,
        IsDeleted = product.IsDeleted,
        SyncStatus = SyncStatus.PendingSync
    };
}
