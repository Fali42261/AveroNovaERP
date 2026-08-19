using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Services;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ICompanySubscriptionService = AveroNova.Application.Interfaces.ICompanySubscriptionService;

namespace AveroNova.App.UI.Services.Local;

public sealed class LocalCompanyService : ICompanyService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ICompanySubscriptionService _subscriptions;

    public LocalCompanyService(
        IDbContextFactory<AppDbContext> factory,
        ICompanySubscriptionService subscriptions)
    {
        _factory = factory;
        _subscriptions = subscriptions;
    }

    public event EventHandler? CurrentCompanyChanged;

    public CompanyModel? CurrentCompany
    {
        get
        {
            using var db = _factory.CreateDbContext();
            var id = LocalSessionStore.CompanyId;
            Company? company = null;
            if (id != null)
            {
                company = db.Companies.AsNoTracking()
                    .FirstOrDefault(c => c.Id == id.Value && !c.IsDeleted);
            }

            if (company == null)
            {
                var userId = LocalSessionStore.UserId;
                if (userId != null)
                {
                    var membership = db.UserCompanies.AsNoTracking()
                        .Where(uc => uc.UserId == userId.Value && uc.IsActive && !uc.IsDeleted)
                        .OrderByDescending(uc => uc.IsOwner)
                        .ThenBy(uc => uc.CreatedAt)
                        .FirstOrDefault();
                    if (membership != null)
                    {
                        company = db.Companies.AsNoTracking()
                            .FirstOrDefault(c => c.Id == membership.CompanyId && !c.IsDeleted);
                    }

                    company ??= db.Companies.AsNoTracking()
                        .FirstOrDefault(c => c.UserId == userId.Value && !c.IsDeleted);
                    if (company != null)
                        LocalSessionStore.Set(userId.Value, company.Id, LocalSessionStore.Email);
                }
            }

            return company == null ? null : Map(company, isCurrent: true);
        }
    }

    public async Task<List<CompanyModel>> GetAllAsync()
    {
        var userId = LocalSessionStore.UserId;
        await using var db = await _factory.CreateDbContextAsync();
        var currentId = LocalSessionStore.CompanyId;
        List<Company> rows;
        if (userId == null)
        {
            rows = [];
        }
        else
        {
            var companyIds = await db.UserCompanies.AsNoTracking()
                .Where(uc => uc.UserId == userId.Value && uc.IsActive && !uc.IsDeleted)
                .Select(uc => uc.CompanyId)
                .ToListAsync();

            var query = db.Companies.AsNoTracking().Where(c => !c.IsDeleted);
            query = companyIds.Count > 0
                ? query.Where(c => companyIds.Contains(c.Id) || c.UserId == userId.Value)
                : query.Where(c => c.UserId == userId.Value);
            rows = await query.ToListAsync();
        }
        return rows.Select(c => Map(c, c.Id == currentId)).ToList();
    }

    public async Task<CompanyModel?> GetByIdAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var company = await db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        return company == null ? null : Map(company, company.Id == LocalSessionStore.CompanyId);
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
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.Companies.FirstOrDefaultAsync(c => c.Id == company.LocalId);
        if (existing == null)
            return (false, "Company not found.");

        existing.CompanyName = company.Name;
        existing.Email = company.Email;
        existing.MobileNumber = company.Phone;
        existing.Address = company.Address;
        existing.City = company.City;
        existing.Country = company.Country;
        existing.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.Companies.FirstOrDefaultAsync(c => c.Id == id);
        if (existing == null)
            return (false, "Company not found.");
        existing.IsDeleted = true;
        existing.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, null);
    }

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

        var snapshot = await _subscriptions.GetCurrentAsync(id);
        if (snapshot == null || snapshot.IsExpired || !snapshot.IsActive)
            return (false, SubscriptionMessages.FreeTrialExpiredAccess);

        LocalSessionStore.Set(userId.Value, id, email);
        CurrentCompanyChanged?.Invoke(this, EventArgs.Empty);
        return (true, null);
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
        SyncStatus = SyncStatus.PendingSync
    };
}
