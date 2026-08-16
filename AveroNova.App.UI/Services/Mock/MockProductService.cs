using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockProductService : IProductService
{
    public Task<List<ProductModel>> GetAllAsync(Guid companyId)
        => Task.FromResult(MockDataStore.Products.Where(p => p.CompanyId == companyId).ToList());

    public Task<ProductModel?> GetByIdAsync(Guid id)
        => Task.FromResult(MockDataStore.Products.FirstOrDefault(p => p.LocalId == id));

    public Task<(bool Ok, string? Error)> CreateAsync(ProductModel product)
    {
        product.LocalId    = Guid.NewGuid();
        product.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Products.Add(product);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> UpdateAsync(ProductModel product)
    {
        var idx = MockDataStore.Products.FindIndex(p => p.LocalId == product.LocalId);
        if (idx < 0) return Task.FromResult((false, "Product not found."));
        product.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Products[idx] = product;
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        var item = MockDataStore.Products.FirstOrDefault(p => p.LocalId == id);
        if (item == null) return Task.FromResult((false, "Product not found."));
        MockDataStore.Products.Remove(item);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<List<ProductModel>> SearchAsync(Guid companyId, string query)
    {
        query = query.ToLowerInvariant();
        var result = MockDataStore.Products
            .Where(p => p.CompanyId == companyId &&
                (p.Name.ToLower().Contains(query) || p.SKU.ToLower().Contains(query) || p.Category.ToLower().Contains(query)))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ProductModel>> GetLowStockAsync(Guid companyId)
        => Task.FromResult(MockDataStore.Products.Where(p => p.CompanyId == companyId && p.IsLowStock).ToList());
}
