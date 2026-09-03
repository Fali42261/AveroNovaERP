using System.Text.RegularExpressions;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Services;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using IAccessControlService = AveroNova.Application.Interfaces.IAccessControlService;
using ICompanySubscriptionService = AveroNova.Application.Interfaces.ICompanySubscriptionService;

namespace AveroNova.App.UI.Services.Local;

public sealed class LocalCompanyService : ICompanyService
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly ICompanyRepository _companies;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ICompanySubscriptionService _subscriptions;
    private readonly IAccessControlService _access;

    public LocalCompanyService(
        ICompanyRepository companies,
        IDbContextFactory<AppDbContext> factory,
        ICompanySubscriptionService subscriptions,
        IAccessControlService access)
    {
        _companies = companies;
        _factory = factory;
        _subscriptions = subscriptions;
        _access = access;
    }

    public event EventHandler? CurrentCompanyChanged;

    public CompanyModel? CurrentCompany
    {
        get
        {
            var userId = LocalSessionStore.UserId;
            var currentId = CurrentCompanyId();
            if (userId == null || currentId == Guid.Empty)
                return null;

            using var db = _factory.CreateDbContext();
            var belongs = db.UserCompanies.Any(
                uc => uc.UserId == userId.Value && uc.CompanyId == currentId && uc.IsActive && !uc.IsDeleted)
                || db.Companies.Any(c => c.Id == currentId && c.UserId == userId.Value && !c.IsDeleted);
            if (!belongs)
                return null;

            var company = db.Companies.AsNoTracking()
                .FirstOrDefault(c => c.Id == currentId && !c.IsDeleted);
            return company == null ? null : Map(company, isCurrent: true);
        }
    }

    public async Task<List<CompanyModel>> GetAllAsync()
    {
        var userId = LocalSessionStore.UserId;
        var currentId = LocalSessionStore.CompanyId;
        if (userId == null)
            return [];

        await using var db = await _factory.CreateDbContextAsync();
        var companyIds = await db.UserCompanies.AsNoTracking()
            .Where(uc => uc.UserId == userId.Value && uc.IsActive && !uc.IsDeleted)
            .Select(uc => uc.CompanyId)
            .ToListAsync();

        var query = db.Companies.AsNoTracking().Where(c => !c.IsDeleted);
        query = companyIds.Count > 0
            ? query.Where(c => companyIds.Contains(c.Id) || c.UserId == userId.Value)
            : query.Where(c => c.UserId == userId.Value);
        var rows = await query.ToListAsync();
        return rows.Select(c => Map(c, c.Id == currentId)).ToList();
    }

    public async Task<CompanyModel?> GetCurrentAsync()
    {
        var userId = LocalSessionStore.UserId;
        var currentId = CurrentCompanyId();
        if (userId == null || currentId == Guid.Empty)
            return null;

        if (!await UserBelongsToCurrentCompanyAsync(userId.Value, currentId))
            return null;

        var company = await _companies.GetByIdAsync(currentId);
        return company == null ? null : Map(company, isCurrent: true);
    }

    public async Task<CompanyModel?> GetByIdAsync(Guid id)
    {
        var userId = LocalSessionStore.UserId;
        var currentId = CurrentCompanyId();
        if (userId == null || currentId == Guid.Empty || id == Guid.Empty || id != currentId)
            return null;

        if (!await UserBelongsToCurrentCompanyAsync(userId.Value, currentId))
            return null;

        var company = await _companies.GetByIdAsync(currentId);
        return company == null ? null : Map(company, isCurrent: true);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(CompanyModel company)
    {
        var userId = LocalSessionStore.UserId;
        if (userId == null)
            return (false, "No authenticated user.");

        await using var db = await _factory.CreateDbContextAsync();
        var entity = new Company
        {
            Id = company.LocalId == Guid.Empty ? Guid.NewGuid() : company.LocalId,
            UserId = userId.Value,
            CompanyCode = "C" + Convert.ToHexString(Guid.NewGuid().ToByteArray())[..8],
            CompanyName = company.Name,
            OwnerName = company.Name,
            Email = company.Email,
            MobileNumber = company.Phone,
            Address = company.Address,
            City = company.City,
            Country = company.Country,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        db.Companies.Add(entity);

        var now = DateTime.UtcNow;
        db.UserCompanies.Add(UserCompanyFactory.CreateOwner(userId.Value, entity.Id, now));

        var freeTrial = await db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Code == SubscriptionPlanCodes.FreeTrial && !p.IsDeleted);
        if (freeTrial != null)
            db.Subscriptions.Add(FreeTrialSubscriptionFactory.Create(entity.Id, freeTrial, now));

        var ownerRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator" && !r.IsDeleted);
        if (ownerRole != null)
        {
            db.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                RoleId = ownerRole.Id,
                CompanyId = entity.Id,
                CreatedAt = now,
                IsDeleted = false
            });
        }

        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(CompanyModel company)
    {
        var userId = LocalSessionStore.UserId;
        var currentId = CurrentCompanyId();
        if (userId == null || currentId == Guid.Empty)
            return (false, "Unable to update company details.");

        var validationError = ValidateUpdate(company);
        if (validationError != null)
            return (false, validationError);

        var snapshot = await _access.GetSnapshotAsync(userId.Value, currentId);
        if (!snapshot.IsMember || !snapshot.Permissions.Contains(PermissionNames.CompanyUpdate))
            return (false, "Unable to update company details.");

        if (!await UserBelongsToCurrentCompanyAsync(userId.Value, currentId))
            return (false, "Unable to update company details.");

        var existing = await _companies.GetByIdAsync(currentId);
        if (existing == null || existing.Id != currentId)
            return (false, "Unable to update company details.");

        // CompanyName and Id are not taken from the UI. CurrentCompanyId is the authority.
        existing.OwnerName = Clamp(company.OwnerName, 150);
        existing.GSTNumber = Clamp(company.TaxNumber, 20);
        existing.PANNumber = Clamp(company.RegistrationNo, 20);
        existing.Email = Clamp(company.Email, 150);
        existing.MobileNumber = Clamp(company.Phone, 15);
        existing.Address = Clamp(company.Address, 500);
        existing.City = Clamp(company.City, 100);
        existing.State = Clamp(company.State, 100);
        existing.Country = Clamp(company.Country, 100);
        existing.PinCode = Clamp(company.PinCode, 10);
        existing.UpdatedAt = DateTime.UtcNow;
        await _companies.UpdateAsync(existing);
        return (true, null);
    }

    public Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
        => Task.FromResult<(bool, string?)>((false, "Company delete is not supported."));

    public async Task<(bool Ok, string? Error)> SwitchCompanyAsync(Guid id)
    {
        var userId = LocalSessionStore.UserId;
        var email = LocalSessionStore.Email;
        if (userId == null)
            return (false, SubscriptionMessages.CompanyContextRequired);

        await using var db = await _factory.CreateDbContextAsync();
        var belongs = await db.UserCompanies.AnyAsync(
            uc => uc.UserId == userId.Value && uc.CompanyId == id && uc.IsActive && !uc.IsDeleted);
        if (!belongs)
        {
            belongs = await db.Companies.AnyAsync(
                c => c.Id == id && c.UserId == userId.Value && !c.IsDeleted);
        }

        if (!belongs)
            return (false, SubscriptionMessages.UserNotInCompany);

        LocalSessionStore.Set(userId.Value, id, email);
        CurrentCompanyChanged?.Invoke(this, EventArgs.Empty);
        return (true, null);
    }

    private static Guid CurrentCompanyId()
        => LocalSessionStore.CompanyId ?? Guid.Empty;

    private async Task<bool> UserBelongsToCurrentCompanyAsync(Guid userId, Guid companyId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var belongs = await db.UserCompanies.AnyAsync(
            uc => uc.UserId == userId && uc.CompanyId == companyId && uc.IsActive && !uc.IsDeleted);
        if (belongs)
            return true;

        return await db.Companies.AnyAsync(
            c => c.Id == companyId && c.UserId == userId && !c.IsDeleted);
    }

    private static string? ValidateUpdate(CompanyModel company)
    {
        if (string.IsNullOrWhiteSpace(company.OwnerName))
            return "Owner name is required";
        if (string.IsNullOrWhiteSpace(company.Email))
            return "Email address is required";
        if (!EmailRegex.IsMatch(company.Email.Trim()))
            return "Please enter a valid email address";
        if (string.IsNullOrWhiteSpace(company.Phone))
            return "Mobile number is required";
        if (company.Phone.Trim().Length > 15)
            return "Mobile number must be 15 characters or fewer";
        return null;
    }

    private static string Clamp(string? value, int maxLength)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static CompanyModel Map(Company company, bool isCurrent) => new()
    {
        LocalId = company.Id,
        Name = company.CompanyName,
        CompanyCode = company.CompanyCode,
        OwnerName = company.OwnerName,
        Email = company.Email,
        Phone = company.MobileNumber,
        Address = company.Address,
        City = company.City,
        State = company.State,
        PinCode = company.PinCode,
        Country = company.Country,
        TaxNumber = company.GSTNumber,
        RegistrationNo = company.PANNumber,
        IsCurrentCompany = isCurrent,
        IsDeleted = company.IsDeleted,
        SyncStatus = SyncStatus.PendingSync
    };
}
