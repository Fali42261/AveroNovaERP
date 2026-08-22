using System.Text.RegularExpressions;
using AveroNova.Application.Interfaces.Repositories;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;

namespace AveroNova.App.UI.Services.Local;

public sealed class LocalCustomerService : ICustomerService
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly ICustomerRepository _customers;
    private readonly CurrentAccessService _access;

    public LocalCustomerService(
        ICustomerRepository customers,
        CurrentAccessService access)
    {
        _customers = customers;
        _access = access;
    }

    public event EventHandler? CustomersChanged;

    public Task<List<CustomerModel>> GetAllAsync(Guid companyId)
        => QueryToListAsync(new CustomerListQuery());

    public async Task<CustomerModel?> GetByIdAsync(Guid id)
    {
        var companyId = CurrentCompanyId();
        if (companyId == Guid.Empty || id == Guid.Empty)
            return null;

        var entity = await _customers.GetByIdAsync(companyId, id);
        return entity == null ? null : Map(entity);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(CustomerModel customer)
    {
        var companyId = CurrentCompanyId();
        if (companyId == Guid.Empty)
            return (false, "Unable to save customer.");

        var error = Validate(customer);
        if (error != null)
            return (false, error);

        var now = DateTime.UtcNow;
        var entity = new Customer
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = Clamp(customer.Name, 200),
            MobileNumber = Clamp(customer.Phone, 15),
            Email = Clamp(customer.Email, 150),
            Address = Clamp(customer.Address, 500),
            City = Clamp(customer.City, 100),
            State = Clamp(customer.State, 100),
            Country = Clamp(customer.Country, 100),
            PinCode = Clamp(customer.PinCode, 10),
            TaxNumber = Clamp(customer.TaxNumber, 50),
            Notes = Clamp(customer.Notes, 1000),
            Status = (int)customer.Status,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        try
        {
            await _customers.AddAsync(entity);
            customer.LocalId = entity.Id;
            customer.CompanyId = companyId;
            customer.CreatedAt = entity.CreatedAt;
            customer.UpdatedAt = entity.UpdatedAt ?? now;
            customer.SyncStatus = SyncStatus.PendingSync;
            RaiseChanged();
            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Customer create failed: {ex.Message}");
            return (false, "Unable to save customer.");
        }
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(CustomerModel customer)
    {
        var companyId = CurrentCompanyId();
        if (companyId == Guid.Empty || customer.LocalId == Guid.Empty)
            return (false, "Unable to save customer.");

        var error = Validate(customer);
        if (error != null)
            return (false, error);

        var existing = await _customers.GetByIdAsync(companyId, customer.LocalId);
        if (existing == null)
            return (false, "Customer not found.");

        existing.Name = Clamp(customer.Name, 200);
        existing.MobileNumber = Clamp(customer.Phone, 15);
        existing.Email = Clamp(customer.Email, 150);
        existing.Address = Clamp(customer.Address, 500);
        existing.City = Clamp(customer.City, 100);
        existing.State = Clamp(customer.State, 100);
        existing.Country = Clamp(customer.Country, 100);
        existing.PinCode = Clamp(customer.PinCode, 10);
        existing.TaxNumber = Clamp(customer.TaxNumber, 50);
        existing.Notes = Clamp(customer.Notes, 1000);
        existing.Status = (int)customer.Status;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.CompanyId = companyId;

        try
        {
            await _customers.UpdateAsync(existing);
            customer.CompanyId = companyId;
            customer.UpdatedAt = existing.UpdatedAt ?? DateTime.UtcNow;
            customer.SyncStatus = SyncStatus.PendingSync;
            RaiseChanged();
            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Customer update failed: {ex.Message}");
            return (false, "Unable to save customer.");
        }
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        var companyId = CurrentCompanyId();
        if (companyId == Guid.Empty || id == Guid.Empty)
            return (false, "Unable to delete customer.");

        var snapshot = await _access.GetSnapshotAsync();
        if (!snapshot.Permissions.Contains(PermissionNames.CustomersManage))
            return (false, "You do not have permission to delete customers.");

        var existing = await _customers.GetByIdAsync(companyId, id);
        if (existing == null)
            return (false, "Customer not found.");

        try
        {
            var deleted = await _customers.SoftDeleteAsync(companyId, id);
            if (!deleted)
                return (false, "Customer not found.");

            RaiseChanged();
            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Customer delete failed: {ex.Message}");
            return (false, "Unable to delete customer.");
        }
    }

    public Task<List<CustomerModel>> SearchAsync(Guid companyId, string query)
        => QueryToListAsync(new CustomerListQuery { SearchText = query });

    public async Task<CustomerListResult> QueryAsync(CustomerListQuery query)
    {
        var companyId = CurrentCompanyId();
        if (companyId == Guid.Empty)
            return new CustomerListResult();

        var status = query.Status.HasValue ? (int?)query.Status.Value : null;
        var (items, total) = await _customers.QueryAsync(
            companyId,
            query.SearchText,
            status,
            query.Skip,
            query.Take);

        return new CustomerListResult
        {
            Items = items.Select(Map).ToList(),
            TotalCount = total
        };
    }

    private async Task<List<CustomerModel>> QueryToListAsync(CustomerListQuery query)
    {
        var result = await QueryAsync(query);
        return result.Items.ToList();
    }

    private void RaiseChanged()
    {
        CustomersChanged?.Invoke(this, EventArgs.Empty);
        CustomerChangeNotifier.Notify();
    }

    private static Guid CurrentCompanyId()
        => LocalSessionStore.CompanyId ?? Guid.Empty;

    private static string? Validate(CustomerModel customer)
    {
        if (string.IsNullOrWhiteSpace(customer.Name))
            return "Customer name is required.";

        var email = customer.Email?.Trim() ?? string.Empty;
        if (email.Length > 0 && !EmailRegex.IsMatch(email))
            return "Please enter a valid email address.";

        var mobile = customer.Phone?.Trim() ?? string.Empty;
        if (mobile.Length > 15)
            return "Mobile number must be 15 characters or fewer.";

        return null;
    }

    private static string Clamp(string? value, int maxLength)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static CustomerModel Map(Customer customer) => new()
    {
        LocalId = customer.Id,
        CompanyId = customer.CompanyId,
        Name = customer.Name,
        Phone = customer.MobileNumber,
        Email = customer.Email,
        Address = customer.Address,
        City = customer.City,
        State = customer.State,
        Country = customer.Country,
        PinCode = customer.PinCode,
        TaxNumber = customer.TaxNumber,
        Notes = customer.Notes,
        Status = Enum.IsDefined(typeof(CustomerStatus), customer.Status)
            ? (CustomerStatus)customer.Status
            : CustomerStatus.Active,
        CreatedAt = customer.CreatedAt,
        UpdatedAt = customer.UpdatedAt ?? customer.CreatedAt,
        IsDeleted = customer.IsDeleted,
        SyncStatus = SyncStatus.Local
    };
}
