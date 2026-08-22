using AveroNova.Application.Interfaces.Repositories;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Local;
using AveroNova.Domain.Entities;

namespace AveroNova.App.UI.Services.Mock;

// Kept under the existing registration name for compatibility, but now uses
// the real local Product repository instead of the in-memory MockDataStore.
public sealed class MockInventoryService : IInventoryService
{
    private readonly IProductRepository _products;

    public MockInventoryService(IProductRepository products)
    {
        _products = products;
    }

    public async Task<List<InventoryItemModel>> GetInventoryAsync(Guid companyId)
    {
        var (items, _) = await _products.QueryAsync(companyId, null, null, 0, 0);
        return items.Select(Map).ToList();
    }

    public async Task<InventoryItemModel?> GetByProductIdAsync(Guid productId)
    {
        if (productId == Guid.Empty)
            return null;

        var product = await FindProductAsync(productId);
        return product == null ? null : Map(product);
    }

    public async Task<List<StockMovementModel>> GetMovementsAsync(Guid companyId, Guid? productId = null)
    {
        // Movement persistence will be introduced with the stock-ledger entity.
        // Returning an empty local result is preferable to showing fake/mock data.
        await Task.CompletedTask;
        return [];
    }

    public async Task<(bool Ok, string? Error)> AdjustStockAsync(StockAdjustmentModel adjustment)
    {
        if (adjustment.CompanyId == Guid.Empty || adjustment.ProductId == Guid.Empty)
            return (false, "Company and product are required.");
        if (adjustment.NewStock < 0)
            return (false, "Stock cannot be negative.");

        var product = await _products.GetByIdAsync(adjustment.CompanyId, adjustment.ProductId);
        if (product == null)
            return (false, "Inventory item not found.");

        product.Stock = adjustment.NewStock;
        product.UpdatedAt = DateTime.UtcNow;

        await _products.UpdateAsync(product);
        return (true, null);
    }

    private async Task<Product?> FindProductAsync(Guid productId)
    {
        var companyId = LocalSessionStore.CompanyId ?? Guid.Empty;
        if (companyId == Guid.Empty)
            return null;
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
        IsDeleted = product.IsDeleted,
        SyncStatus = SyncStatus.PendingSync
    };
}
