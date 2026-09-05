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
    private readonly IConnectivityService _connectivity;
    private readonly ISyncService _sync;

    public LocalCompanyService(
        IDbContextFactory<LocalAppDbContext> dbFactory,
        IAppSessionContext session,
        ILocalAuthSessionStore sessions,
        IInstallationService installation,
        IConnectivityService connectivity,
        ISyncService sync)
    {
        _dbFactory = dbFactory;
        _session = session;
        _sessions = sessions;
        _installation = installation;
        _connectivity = connectivity;
        _sync = sync;
    }

    public CompanyModel? CurrentCompany
        => _session.CurrentCompany is null ? null : Map(_session.CurrentCompany.Id, _session.CurrentCompany.CompanyName, _session.CurrentCompany.Email, _session.CurrentCompany.MobileNumber, isCurrent: true);

    public async Task<List<CompanyModel>> GetAllAsync()
    {
        if (_session.CurrentUserId is not Guid userId)
            return [];

        var companies = await _sessions.GetCompaniesForUserAsync(userId);
        return companies.Select(c => Map(c.Id, c.CompanyName, c.Email, c.MobileNumber, c.Id == _session.CurrentCompanyId)).ToList();
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
        var row = new LocalCompanyEntity
        {
            Id = company.LocalId,
            CompanyName = company.Name.Trim(),
            Email = company.Email.Trim(),
            MobileNumber = company.Phone.Trim(),
            IsActive = true,
            SyncVersion = 1
        };
        db.Companies.Add(row);
        db.UserCompanies.Add(new LocalUserCompanyEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CompanyId = company.LocalId,
            IsDefault = false,
            IsOwner = true,
            IsActive = true
        });
        LocalSyncQueueWriter.Enqueue(db, "Company", row.Id, row.Id, SyncOperation.Create, Payload(row), now);
        await db.SaveChangesAsync();
        _connectivity.IncrementPending();
        TriggerSyncIfOnline();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(CompanyModel company)
    {
        if (!Owns(company.LocalId))
            return (false, "You do not have access to this company.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Companies.FirstOrDefaultAsync(c => c.Id == company.LocalId);
        if (row is null)
            return (false, "Company not found.");

        row.CompanyName = company.Name.Trim();
        row.Email = company.Email.Trim();
        row.MobileNumber = company.Phone.Trim();
        row.SyncVersion = Math.Max(1, row.SyncVersion + 1);
        LocalSyncQueueWriter.Enqueue(db, "Company", row.Id, row.Id, SyncOperation.Update, Payload(row), DateTime.UtcNow);
        await db.SaveChangesAsync();
        _connectivity.IncrementPending();
        TriggerSyncIfOnline();
        return (true, null);
    }

    public Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
        => Task.FromResult<(bool, string?)>((false, "Deleting a company is not available offline."));

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

    private void TriggerSyncIfOnline()
    {
        if (_connectivity.IsOnline)
            _ = _sync.SyncNowAsync();
    }

    private bool Owns(Guid companyId)
        => _session.CurrentUserId is Guid && _session.CurrentCompanyId == companyId;

    private static object Payload(LocalCompanyEntity row) => new
    {
        id = row.Id,
        companyName = row.CompanyName,
        email = row.Email,
        mobileNumber = row.MobileNumber,
        syncVersion = row.SyncVersion
    };

    private static CompanyModel Map(Guid id, string name, string email, string phone, bool isCurrent)
        => new()
        {
            LocalId = id,
            Name = name,
            Email = email,
            Phone = phone,
            IsCurrentCompany = isCurrent,
            SyncStatus = SyncStatus.Local
        };
}
