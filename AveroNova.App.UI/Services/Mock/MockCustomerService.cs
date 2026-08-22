using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockCustomerService : ICustomerService
{
    public event EventHandler? CustomersChanged;

    public Task<List<CustomerModel>> GetAllAsync(Guid companyId)
        => Task.FromResult(MockDataStore.Customers.Where(c => c.CompanyId == companyId && !c.IsDeleted).ToList());

    public Task<CustomerModel?> GetByIdAsync(Guid id)
        => Task.FromResult(MockDataStore.Customers.FirstOrDefault(c => c.LocalId == id && !c.IsDeleted));

    public Task<(bool Ok, string? Error)> CreateAsync(CustomerModel customer)
    {
        customer.LocalId = Guid.NewGuid();
        customer.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Customers.Add(customer);
        CustomersChanged?.Invoke(this, EventArgs.Empty);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> UpdateAsync(CustomerModel customer)
    {
        var idx = MockDataStore.Customers.FindIndex(c => c.LocalId == customer.LocalId);
        if (idx < 0)
            return Task.FromResult<(bool Ok, string? Error)>((false, "Customer not found."));
        customer.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Customers[idx] = customer;
        CustomersChanged?.Invoke(this, EventArgs.Empty);
        return Task.FromResult<(bool Ok, string? Error)>((true, null));
    }

    public Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        var item = MockDataStore.Customers.FirstOrDefault(c => c.LocalId == id && !c.IsDeleted);
        if (item == null)
            return Task.FromResult<(bool Ok, string? Error)>((false, "Customer not found."));
        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;
        item.SyncStatus = SyncStatus.PendingSync;
        CustomersChanged?.Invoke(this, EventArgs.Empty);
        return Task.FromResult<(bool Ok, string? Error)>((true, null));
    }

    public Task<List<CustomerModel>> SearchAsync(Guid companyId, string query)
        => QueryToListAsync(companyId, new CustomerListQuery { SearchText = query });

    public Task<CustomerListResult> QueryAsync(CustomerListQuery query)
        => QueryAsync(Guid.Empty, query);

    private Task<CustomerListResult> QueryAsync(Guid companyId, CustomerListQuery query)
    {
        IEnumerable<CustomerModel> source = MockDataStore.Customers.Where(c => !c.IsDeleted);
        if (companyId != Guid.Empty)
            source = source.Where(c => c.CompanyId == companyId);

        var term = query.SearchText?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(term))
        {
            source = source.Where(c =>
                c.Name.ToLower().Contains(term)
                || c.Email.ToLower().Contains(term)
                || c.Phone.ToLower().Contains(term));
        }

        if (query.Status.HasValue)
            source = source.Where(c => c.Status == query.Status.Value);

        var matched = source.OrderBy(c => c.Name).ToList();
        var total = matched.Count;
        if (query.Skip > 0)
            matched = matched.Skip(query.Skip).ToList();
        if (query.Take > 0)
            matched = matched.Take(query.Take).ToList();

        return Task.FromResult(new CustomerListResult { Items = matched, TotalCount = total });
    }

    private async Task<List<CustomerModel>> QueryToListAsync(Guid companyId, CustomerListQuery query)
    {
        var result = await QueryAsync(companyId, query);
        return result.Items.ToList();
    }
}
