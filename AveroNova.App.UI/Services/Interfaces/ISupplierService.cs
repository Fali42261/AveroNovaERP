using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

public interface ISupplierService
{
    Task<List<SupplierModel>> GetAllAsync(Guid companyId);
    Task<SupplierModel?> GetByIdAsync(Guid id);
    Task<(bool Ok, string? Error)> CreateAsync(SupplierModel supplier);
    Task<(bool Ok, string? Error)> UpdateAsync(SupplierModel supplier);
    Task<(bool Ok, string? Error)> DeleteAsync(Guid id);
}
