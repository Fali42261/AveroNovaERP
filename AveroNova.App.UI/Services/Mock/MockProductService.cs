using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockProductService : IProductService
{
    public event EventHandler? ProductsChanged;

    public Task<List<ProductModel>> GetAllAsync(Guid companyId)
        => Task.FromResult(MockDataStore.Products.Where(p => p.CompanyId == companyId && !p.IsDeleted).ToList());

    public Task<ProductModel?> GetByIdAsync(Guid id)
        => Task.FromResult(MockDataStore.Products.FirstOrDefault(p => p.LocalId == id && !p.IsDeleted));

    public Task<(bool Ok, string? Error)> CreateAsync(ProductModel product)
    {
        product.LocalId = Guid.NewGuid();
        product.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Products.Add(product);
        ProductsChanged?.Invoke(this, EventArgs.Empty);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> UpdateAsync(ProductModel product)
    {
        var idx = MockDataStore.Products.FindIndex(p => p.LocalId == product.LocalId);
        if (idx < 0) return Task.FromResult((false, "Product not found."));
        product.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Products[idx] = product;
        ProductsChanged?.Invoke(this, EventArgs.Empty);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        var item = MockDataStore.Products.FirstOrDefault(p => p.LocalId == id && !p.IsDeleted);
        if (item == null) return Task.FromResult((false, "Product not found."));
        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;
        item.SyncStatus = SyncStatus.PendingSync;
        ProductsChanged?.Invoke(this, EventArgs.Empty);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<List<ProductModel>> SearchAsync(Guid companyId, string query)
    {
        query = query.ToLowerInvariant();
        var result = MockDataStore.Products
            .Where(p => p.CompanyId == companyId && !p.IsDeleted &&
                (p.Name.ToLower().Contains(query) || p.SKU.ToLower().Contains(query) || p.Barcode.ToLower().Contains(query)))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ProductModel>> GetLowStockAsync(Guid companyId)
        => Task.FromResult(MockDataStore.Products.Where(p => p.CompanyId == companyId && !p.IsDeleted && p.IsLowStock).ToList());

    public Task<ProductListResult> QueryAsync(ProductListQuery query)
    {
        IEnumerable<ProductModel> source = MockDataStore.Products.Where(p => !p.IsDeleted);
        if (query.Status.HasValue)
            source = source.Where(p => p.Status == query.Status.Value);

        var term = query.SearchText?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(term))
        {
            source = source.Where(p =>
                p.Name.ToLower().Contains(term)
                || p.SKU.ToLower().Contains(term)
                || p.Barcode.ToLower().Contains(term));
        }

        var list = source.OrderBy(p => p.Name).ToList();
        var total = list.Count;
        if (query.Skip > 0)
            list = list.Skip(query.Skip).ToList();
        if (query.Take > 0)
            list = list.Take(query.Take).ToList();

        return Task.FromResult(new ProductListResult
        {
            Items = list,
            TotalCount = total
        });
    }
}
