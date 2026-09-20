using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public sealed class LocalCompanyService : ICompanyService
{
    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly IAppSessionContext _session;
    private readonly ILocalAuthSessionStore _sessions;
    private readonly IInstallationService _installation;

    public LocalCompanyService(
        IDbContextFactory<LocalAppDbContext> dbFactory,
        IAppSessionContext session,
        ILocalAuthSessionStore sessions,
        IInstallationService installation)
    {
        _dbFactory = dbFactory;
        _session = session;
        _sessions = sessions;
        _installation = installation;
    }

    public CompanyModel? CurrentCompany
        => _session.CurrentCompany is null ? null : new CompanyModel
        {
            LocalId = _session.CurrentCompany.Id,
            Name = _session.CurrentCompany.CompanyName,
            Email = _session.CurrentCompany.Email,
            Phone = _session.CurrentCompany.MobileNumber,
            IsCurrentCompany = true,
            SyncStatus = SyncStatus.Local
        };

    public async Task<List<CompanyModel>> GetAllAsync()
    {
        if (_session.CurrentUserId is not Guid userId)
            return [];

        var companies = await _sessions.GetCompaniesForUserAsync(userId);
        return companies.Select(c => Map(c, c.Id == _session.CurrentCompanyId)).ToList();
    }

    public async Task<CompanyModel?> GetByIdAsync(Guid id)
    {
        var all = await GetAllAsync();
        return all.FirstOrDefault(c => c.LocalId == id);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(CompanyModel company)
    {
        if (_session.CurrentUserId is not Guid userId)
            return (false, "Sign in to create a company.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        company.LocalId = company.LocalId == Guid.Empty ? Guid.NewGuid() : company.LocalId;
        db.Companies.Add(new LocalCompanyEntity
        {
            Id = company.LocalId,
            CompanyName = company.Name.Trim(),
            Email = company.Email.Trim(),
            MobileNumber = company.Phone.Trim(),
            Address = company.Address.Trim(),
            City = company.City.Trim(),
            Country = company.Country.Trim(),
            TaxNumber = company.TaxNumber.Trim(),
            RegistrationNo = company.RegistrationNo.Trim(),
            Currency = string.IsNullOrWhiteSpace(company.Currency) ? "USD" : company.Currency.Trim(),
            CurrencySymbol = company.CurrencySymbol,
            LogoUrl = company.LogoUrl.Trim(),
            InvoicePrefix = string.IsNullOrWhiteSpace(company.InvoicePrefix) ? "INV" : company.InvoicePrefix.Trim(),
            Website = company.Website.Trim(),
            IsActive = true
        });
        db.UserCompanies.Add(new LocalUserCompanyEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CompanyId = company.LocalId,
            IsDefault = false,
            IsOwner = true,
            IsActive = true
        });
        LocalSyncQueueWriter.Enqueue(db, "Company", company.LocalId, company.LocalId, SyncOperation.Create,
            new { company.Name, company.Email, company.Phone, company.Address, company.City, company.Country, company.TaxNumber, company.RegistrationNo, company.Currency, company.CurrencySymbol, company.InvoicePrefix, company.Website }, now);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(CompanyModel company)
    {
        if (_session.CurrentUserId is not Guid userId)
            return (false, "You do not have access to this company.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var canEdit = await db.UserCompanies.AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.CompanyId == company.LocalId && x.IsActive);
        if (!canEdit)
            return (false, "You do not have access to this company.");
        var row = await db.Companies.FirstOrDefaultAsync(c => c.Id == company.LocalId);
        if (row is null)
            return (false, "Company not found.");

        row.CompanyName = company.Name.Trim();
        row.Email = company.Email.Trim();
        row.MobileNumber = company.Phone.Trim();
        row.Address = company.Address.Trim();
        row.City = company.City.Trim();
        row.Country = company.Country.Trim();
        row.TaxNumber = company.TaxNumber.Trim();
        row.RegistrationNo = company.RegistrationNo.Trim();
        row.Currency = string.IsNullOrWhiteSpace(company.Currency) ? "USD" : company.Currency.Trim();
        row.CurrencySymbol = company.CurrencySymbol;
        row.LogoUrl = company.LogoUrl.Trim();
        row.InvoicePrefix = string.IsNullOrWhiteSpace(company.InvoicePrefix) ? "INV" : company.InvoicePrefix.Trim();
        row.Website = company.Website.Trim();
        LocalSyncQueueWriter.Enqueue(db, "Company", row.Id, row.Id, SyncOperation.Update,
            new { row.CompanyName, row.Email, row.MobileNumber, row.Address, row.City, row.Country, row.TaxNumber, row.RegistrationNo, row.Currency, row.CurrencySymbol, row.InvoicePrefix, row.Website }, DateTime.UtcNow);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        if (_session.CurrentUserId is not Guid userId)
            return (false, "Sign in to delete a company.");
        if (_session.CurrentCompanyId == id)
            return (false, "Switch to another company before deleting the current company.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var membership = await db.UserCompanies.FirstOrDefaultAsync(x =>
            x.UserId == userId && x.CompanyId == id && x.IsActive);
        if (membership is null || !membership.IsOwner)
            return (false, "Only a company owner can delete this company.");

        var company = await db.Companies.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (company is null)
            return (false, "Company not found.");

        company.IsActive = false;
        var memberships = await db.UserCompanies.Where(x => x.CompanyId == id).ToListAsync();
        foreach (var item in memberships)
            item.IsActive = false;

        LocalSyncQueueWriter.Enqueue(db, "Company", id, id, SyncOperation.Delete,
            new { Id = id }, DateTime.UtcNow);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task SwitchCompanyAsync(Guid id)
    {
        if (_session.CurrentUserId is not Guid userId)
            return;

        await _installation.EnsureInitializedAsync();
        var snapshot = await _sessions.SwitchCompanyAsync(_installation.InstallationId, userId, id);
        if (snapshot is null)
            return;

        _session.SetFromLocal(
            snapshot.User,
            snapshot.Company,
            snapshot.Roles,
            snapshot.Permissions,
            snapshot.Session.ServerSessionId);
    }

    private static CompanyModel Map(LocalCompanyEntity company, bool isCurrent)
        => new()
        {
            LocalId = company.Id,
            Name = company.CompanyName,
            Email = company.Email,
            Phone = company.MobileNumber,
            Address = company.Address,
            City = company.City,
            Country = company.Country,
            TaxNumber = company.TaxNumber,
            RegistrationNo = company.RegistrationNo,
            Currency = company.Currency,
            CurrencySymbol = company.CurrencySymbol,
            LogoUrl = company.LogoUrl,
            InvoicePrefix = company.InvoicePrefix,
            Website = company.Website,
            Status = company.IsActive ? CompanyStatus.Active : CompanyStatus.Inactive,
            IsCurrentCompany = isCurrent,
            SyncStatus = SyncStatus.Local
        };
}
