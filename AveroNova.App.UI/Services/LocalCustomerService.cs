using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public sealed class LocalCustomerService : ICustomerService
{
    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly IAppSessionContext _session;
    private readonly IConnectivityService _connectivity;
    private readonly ISyncService _sync;

    public LocalCustomerService(
        IDbContextFactory<LocalAppDbContext> dbFactory,
        IAppSessionContext session,
        IConnectivityService connectivity,
        ISyncService sync)
    {
        _dbFactory = dbFactory;
        _session = session;
        _connectivity = connectivity;
        _sync = sync;
    }

    public async Task<List<CustomerModel>> GetAllAsync(Guid companyId)
    {
        if (!Allows(companyId)) return [];
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.Customers.AsNoTracking().Where(c => c.CompanyId == companyId).OrderBy(c => c.Name).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<CustomerModel?> GetByIdAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return row is null || !Allows(row.CompanyId) ? null : Map(row);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(CustomerModel customer)
    {
        if (!Allows(customer.CompanyId)) return (false, "You do not have access to this company.");
        if (string.IsNullOrWhiteSpace(customer.Name)) return (false, "Customer name is required.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        customer.LocalId = customer.LocalId == Guid.Empty ? Guid.NewGuid() : customer.LocalId;
        var row = ToEntity(customer, now);
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        db.Customers.Add(row);
        LocalSyncQueueWriter.Enqueue(db, "Customer", row.Id, row.CompanyId, SyncOperation.Create, Payload(row), now);
        await db.SaveChangesAsync();
        _connectivity.IncrementPending();
        TriggerSyncIfOnline();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(CustomerModel customer)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Customers.FirstOrDefaultAsync(c => c.Id == customer.LocalId);
        if (row is null || !Allows(row.CompanyId)) return (false, "Customer not found.");

        var now = DateTime.UtcNow;
        Apply(row, customer, now);
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        LocalSyncQueueWriter.Enqueue(db, "Customer", row.Id, row.CompanyId, SyncOperation.Update, Payload(row), now);
        await db.SaveChangesAsync();
        _connectivity.IncrementPending();
        TriggerSyncIfOnline();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Customers.FirstOrDefaultAsync(c => c.Id == id);
        if (row is null || !Allows(row.CompanyId)) return (false, "Customer not found.");

        var companyId = row.CompanyId;
        db.Customers.Remove(row);
        LocalSyncQueueWriter.Enqueue(db, "Customer", row.Id, companyId, SyncOperation.Delete,
            new { row.Id, CompanyId = companyId }, DateTime.UtcNow);
        await db.SaveChangesAsync();
        _connectivity.IncrementPending();
        TriggerSyncIfOnline();
        return (true, null);
    }

    public async Task<List<CustomerModel>> SearchAsync(Guid companyId, string query)
    {
        var all = await GetAllAsync(companyId);
        if (string.IsNullOrWhiteSpace(query)) return all;
        return all.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                           || c.Email.Contains(query, StringComparison.OrdinalIgnoreCase)
                           || c.Phone.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void TriggerSyncIfOnline()
    {
        if (_connectivity.IsOnline) _ = _sync.SyncNowAsync();
    }

    private bool Allows(Guid companyId)
        => _session.CurrentCompanyId is Guid current && current != Guid.Empty && current == companyId;

    private static CustomerModel Map(LocalCustomerEntity row) => new()
    {
        LocalId = row.Id, ServerId = row.ServerId?.ToString("D"), CompanyId = row.CompanyId,
        Name = row.Name, Email = row.Email, Phone = row.Phone, Address = row.Address, City = row.City,
        Country = row.Country, TaxNumber = row.TaxNumber, Notes = row.Notes, Status = (CustomerStatus)row.Status,
        OutstandingBalance = row.OutstandingBalance, TotalPurchases = row.TotalPurchases,
        CreatedAt = row.CreatedAtUtc, UpdatedAt = row.UpdatedAtUtc, LastSyncedAt = row.LastSyncedAtUtc,
        SyncStatus = ToUiStatus(row.SyncStatus)
    };

    private static LocalCustomerEntity ToEntity(CustomerModel model, DateTime now) => new()
    {
        Id = model.LocalId, CompanyId = model.CompanyId, Name = model.Name.Trim(), Email = model.Email.Trim(),
        Phone = model.Phone.Trim(), Address = model.Address, City = model.City, Country = model.Country,
        TaxNumber = model.TaxNumber, Notes = model.Notes, Status = (int)model.Status,
        OutstandingBalance = model.OutstandingBalance, TotalPurchases = model.TotalPurchases,
        CreatedAtUtc = now, UpdatedAtUtc = now
    };

    private static void Apply(LocalCustomerEntity row, CustomerModel model, DateTime now)
    {
        row.Name = model.Name.Trim(); row.Email = model.Email.Trim(); row.Phone = model.Phone.Trim();
        row.Address = model.Address; row.City = model.City; row.Country = model.Country; row.TaxNumber = model.TaxNumber;
        row.Notes = model.Notes; row.Status = (int)model.Status; row.OutstandingBalance = model.OutstandingBalance;
        row.TotalPurchases = model.TotalPurchases; row.UpdatedAtUtc = now; row.SyncError = null;
    }

    private static object Payload(LocalCustomerEntity row) => new
    {
        row.Id, row.CompanyId, row.Name, row.Email, row.Phone, row.Address, row.City, row.Country,
        row.TaxNumber, row.Notes, row.Status, row.OutstandingBalance, row.TotalPurchases, SyncVersion = 1L
    };

    private static SyncStatus ToUiStatus(int status) => (RecordSyncStatus)status switch
    {
        RecordSyncStatus.Synced => SyncStatus.Synced,
        RecordSyncStatus.Failed => SyncStatus.SyncFailed,
        _ => SyncStatus.PendingSync
    };
}
