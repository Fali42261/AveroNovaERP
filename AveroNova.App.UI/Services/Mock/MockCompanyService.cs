using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockCompanyService : ICompanyService
{
    public event EventHandler? CurrentCompanyChanged;

    public CompanyModel? CurrentCompany
        => MockDataStore.Companies.FirstOrDefault(c => c.IsCurrentCompany && !c.IsDeleted)
        ?? MockDataStore.Companies.FirstOrDefault(c => !c.IsDeleted);

    public Task<List<CompanyModel>> GetAllAsync()
        => Task.FromResult(MockDataStore.Companies.Where(c => !c.IsDeleted).ToList());

    public Task<CompanyModel?> GetCurrentAsync()
        => Task.FromResult(MockDataStore.Companies.FirstOrDefault(c => c.IsCurrentCompany && !c.IsDeleted));

    public Task<CompanyModel?> GetByIdAsync(Guid id)
        => Task.FromResult(MockDataStore.Companies.FirstOrDefault(c => c.LocalId == id && !c.IsDeleted));

    public Task<(bool Ok, string? Error)> CreateAsync(CompanyModel company)
    {
        company.LocalId    = Guid.NewGuid();
        company.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Companies.Add(company);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> UpdateAsync(CompanyModel company)
    {
        var current = CurrentCompany;
        if (current == null)
            return Task.FromResult((false, "Unable to update company details."));

        var existing = MockDataStore.Companies.FirstOrDefault(c => c.LocalId == current.LocalId);
        if (existing == null)
            return Task.FromResult((false, "Unable to update company details."));

        existing.OwnerName = company.OwnerName;
        existing.Email = company.Email;
        existing.Phone = company.Phone;
        existing.Address = company.Address;
        existing.City = company.City;
        existing.State = company.State;
        existing.PinCode = company.PinCode;
        existing.Country = company.Country;
        existing.TaxNumber = company.TaxNumber;
        existing.RegistrationNo = company.RegistrationNo;
        existing.SyncStatus = SyncStatus.PendingSync;
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        var item = MockDataStore.Companies.FirstOrDefault(c => c.LocalId == id && !c.IsDeleted);
        if (item == null) return Task.FromResult((false, "Company not found."));
        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;
        item.IsCurrentCompany = false;
        item.SyncStatus = SyncStatus.PendingSync;
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> SwitchCompanyAsync(Guid id)
    {
        foreach (var c in MockDataStore.Companies) c.IsCurrentCompany = c.LocalId == id;
        CurrentCompanyChanged?.Invoke(this, EventArgs.Empty);
        return Task.FromResult<(bool, string?)>((true, null));
    }
}
