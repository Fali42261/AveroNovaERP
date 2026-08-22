using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

public interface ICustomerService
{
    event EventHandler? CustomersChanged;

    /// <summary>
    /// Returns customers for the current company only.
    /// <paramref name="companyId"/> is not trusted; current company context is always used.
    /// </summary>
    Task<List<CustomerModel>> GetAllAsync(Guid companyId);

    Task<CustomerModel?> GetByIdAsync(Guid id);

    Task<(bool Ok, string? Error)> CreateAsync(CustomerModel customer);

    Task<(bool Ok, string? Error)> UpdateAsync(CustomerModel customer);

    Task<(bool Ok, string? Error)> DeleteAsync(Guid id);

    /// <summary>
    /// Searches the current company's customers by name, mobile, or email.
    /// <paramref name="companyId"/> is not trusted; current company context is always used.
    /// </summary>
    Task<List<CustomerModel>> SearchAsync(Guid companyId, string query);

    Task<CustomerListResult> QueryAsync(CustomerListQuery query);
}
