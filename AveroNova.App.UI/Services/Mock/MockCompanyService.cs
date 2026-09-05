using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockCompanyService : ICompanyService
{
    public CompanyModel? CurrentCompany
        => MockDataStore.Companies.FirstOrDefault(c => c.IsCurrentCompany)
        ?? MockDataStore.Companies.FirstOrDefault();

    public Task<List<CompanyModel>> GetAllAsync()
        => Task.FromResult(MockDataStore.Companies.ToList());

    public Task<CompanyModel?> GetByIdAsync(Guid id)
        => Task.FromResult(MockDataStore.Companies.FirstOrDefault(c => c.LocalId == id));

    public Task<(bool Ok, string? Error)> CreateAsync(CompanyModel company)
    {
        company.LocalId    = Guid.NewGuid();
        company.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Companies.Add(company);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> UpdateAsync(CompanyModel company)
    {
        var existing = MockDataStore.Companies.FirstOrDefault(c => c.LocalId == company.LocalId);
        if (existing == null) return Task.FromResult((false, "Company not found."));
        var idx = MockDataStore.Companies.IndexOf(existing);
        company.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Companies[idx] = company;
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        var item = MockDataStore.Companies.FirstOrDefault(c => c.LocalId == id);
        if (item == null) return Task.FromResult((false, "Company not found."));
        MockDataStore.Companies.Remove(item);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task SwitchCompanyAsync(Guid id)
    {
        foreach (var c in MockDataStore.Companies) c.IsCurrentCompany = c.LocalId == id;
        return Task.CompletedTask;
    }
}
