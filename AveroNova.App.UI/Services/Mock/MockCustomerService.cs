using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockCustomerService : ICustomerService
{
    public Task<List<CustomerModel>> GetAllAsync(Guid companyId)
        => Task.FromResult(MockDataStore.Customers.Where(c => c.CompanyId == companyId).ToList());

    public Task<CustomerModel?> GetByIdAsync(Guid id)
        => Task.FromResult(MockDataStore.Customers.FirstOrDefault(c => c.LocalId == id));

    public Task<(bool Ok, string? Error)> CreateAsync(CustomerModel customer)
    {
        customer.LocalId    = Guid.NewGuid();
        customer.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Customers.Add(customer);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> UpdateAsync(CustomerModel customer)
    {
        var idx = MockDataStore.Customers.FindIndex(c => c.LocalId == customer.LocalId);
        if (idx < 0) return Task.FromResult((false, "Customer not found."));
        customer.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Customers[idx] = customer;
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        var item = MockDataStore.Customers.FirstOrDefault(c => c.LocalId == id);
        if (item == null) return Task.FromResult((false, "Customer not found."));
        MockDataStore.Customers.Remove(item);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<List<CustomerModel>> SearchAsync(Guid companyId, string query)
    {
        query = query.ToLowerInvariant();
        var result = MockDataStore.Customers
            .Where(c => c.CompanyId == companyId &&
                (c.Name.ToLower().Contains(query) || c.Email.ToLower().Contains(query) || c.Phone.Contains(query)))
            .ToList();
        return Task.FromResult(result);
    }
}
