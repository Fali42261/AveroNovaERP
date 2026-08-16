using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

public interface IProductService
{
    Task<List<ProductModel>> GetAllAsync(Guid companyId);
    Task<ProductModel?>      GetByIdAsync(Guid id);
    Task<(bool Ok, string? Error)> CreateAsync(ProductModel product);
    Task<(bool Ok, string? Error)> UpdateAsync(ProductModel product);
    Task<(bool Ok, string? Error)> DeleteAsync(Guid id);
    Task<List<ProductModel>> SearchAsync(Guid companyId, string query);
    Task<List<ProductModel>> GetLowStockAsync(Guid companyId);
}
