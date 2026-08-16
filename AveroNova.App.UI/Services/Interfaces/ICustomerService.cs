using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

public interface ICustomerService
{
    Task<List<CustomerModel>> GetAllAsync(Guid companyId);
    Task<CustomerModel?>      GetByIdAsync(Guid id);
    Task<(bool Ok, string? Error)> CreateAsync(CustomerModel customer);
    Task<(bool Ok, string? Error)> UpdateAsync(CustomerModel customer);
    Task<(bool Ok, string? Error)> DeleteAsync(Guid id);
    Task<List<CustomerModel>> SearchAsync(Guid companyId, string query);
}
