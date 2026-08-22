using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

public interface IProductService
{
    event EventHandler? ProductsChanged;

    /// <summary>
    /// Returns products for the current company only.
    /// <paramref name="companyId"/> is not trusted; current company context is always used.
    /// </summary>
    Task<List<ProductModel>> GetAllAsync(Guid companyId);

    Task<ProductModel?> GetByIdAsync(Guid id);

    Task<(bool Ok, string? Error)> CreateAsync(ProductModel product);

    Task<(bool Ok, string? Error)> UpdateAsync(ProductModel product);

    Task<(bool Ok, string? Error)> DeleteAsync(Guid id);

    /// <summary>
    /// Searches the current company's products by name, SKU, or barcode.
    /// <paramref name="companyId"/> is not trusted; current company context is always used.
    /// </summary>
    Task<List<ProductModel>> SearchAsync(Guid companyId, string query);

    /// <summary>
    /// Returns low-stock products for the current company only.
    /// <paramref name="companyId"/> is not trusted; current company context is always used.
    /// </summary>
    Task<List<ProductModel>> GetLowStockAsync(Guid companyId);

    Task<ProductListResult> QueryAsync(ProductListQuery query);
}
