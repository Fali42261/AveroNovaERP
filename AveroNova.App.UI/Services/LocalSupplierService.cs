using System.Net.Mail;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public sealed class LocalSupplierService : ISupplierService
{
    private readonly IDbContextFactory<LocalAppDbContext> _factory;
    private readonly IAppSessionContext _session;

    public LocalSupplierService(IDbContextFactory<LocalAppDbContext> factory, IAppSessionContext session)
    { _factory = factory; _session = session; }

    public async Task<List<SupplierModel>> GetAllAsync(Guid companyId)
    {
        if (!Allows(companyId)) return [];
        await using var db = await _factory.CreateDbContextAsync();
        return (await db.Suppliers.AsNoTracking().Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Name).ToListAsync()).Select(Map).ToList();
    }

    public async Task<SupplierModel?> GetByIdAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return row is null || !Allows(row.CompanyId) ? null : Map(row);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(SupplierModel model)
    {
        var error = Validate(model); if (error is not null) return (false, error);
        await using var db = await _factory.CreateDbContextAsync();
        if (await db.Suppliers.AnyAsync(x => x.CompanyId == model.CompanyId && x.Name.ToLower() == model.Name.Trim().ToLower()))
            return (false, "A supplier with this name already exists.");
        var now = DateTime.UtcNow;
        model.LocalId = model.LocalId == Guid.Empty ? Guid.NewGuid() : model.LocalId;
        var row = ToEntity(model, now); db.Suppliers.Add(row);
        LocalSyncQueueWriter.Enqueue(db, "Supplier", row.Id, row.CompanyId, SyncOperation.Create, Payload(row), now);
        await db.SaveChangesAsync(); return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(SupplierModel model)
    {
        var error = Validate(model); if (error is not null) return (false, error);
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == model.LocalId);
        if (row is null || !Allows(row.CompanyId) || row.CompanyId != model.CompanyId) return (false, "Supplier not found.");
        if (await db.Suppliers.AnyAsync(x => x.CompanyId == model.CompanyId && x.Id != model.LocalId && x.Name.ToLower() == model.Name.Trim().ToLower()))
            return (false, "A supplier with this name already exists.");
        Apply(row, model, DateTime.UtcNow);
        LocalSyncQueueWriter.Enqueue(db, "Supplier", row.Id, row.CompanyId, SyncOperation.Update, Payload(row), row.UpdatedAtUtc);
        await db.SaveChangesAsync(); return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id);
        if (row is null || !Allows(row.CompanyId)) return (false, "Supplier not found.");
        if (await db.Purchases.AnyAsync(x => x.CompanyId == row.CompanyId && x.SupplierId == id))
            return (false, "Supplier cannot be deleted because purchases reference it.");
        db.Suppliers.Remove(row);
        LocalSyncQueueWriter.Enqueue(db, "Supplier", id, row.CompanyId, SyncOperation.Delete, new { row.Id, row.CompanyId }, DateTime.UtcNow);
        await db.SaveChangesAsync(); return (true, null);
    }

    private string? Validate(SupplierModel m)
    {
        if (!Allows(m.CompanyId)) return "You do not have access to this company.";
        if (string.IsNullOrWhiteSpace(m.Name)) return "Supplier name is required.";
        if (m.Name.Trim().Length > 200) return "Supplier name must be 200 characters or fewer.";
        if (!string.IsNullOrWhiteSpace(m.Email)) { try { _ = new MailAddress(m.Email.Trim()); } catch { return "Enter a valid supplier email."; } }
        return null;
    }

    private bool Allows(Guid id) => id != Guid.Empty && _session.CurrentCompanyId == id;
    private static SupplierModel Map(LocalSupplierEntity x) => new() { LocalId=x.Id, ServerId=x.ServerId?.ToString("D"), CompanyId=x.CompanyId, Name=x.Name, Email=x.Email, Phone=x.Phone, Address=x.Address, TaxNumber=x.TaxNumber, Notes=x.Notes, IsActive=x.IsActive, SyncStatus=ToUi(x.SyncStatus), CreatedAt=x.CreatedAtUtc, UpdatedAt=x.UpdatedAtUtc, LastSyncedAt=x.LastSyncedAtUtc };
    private static LocalSupplierEntity ToEntity(SupplierModel m, DateTime now) => new() { Id=m.LocalId, CompanyId=m.CompanyId, Name=m.Name.Trim(), Email=m.Email.Trim(), Phone=m.Phone.Trim(), Address=m.Address.Trim(), TaxNumber=m.TaxNumber.Trim(), Notes=m.Notes.Trim(), IsActive=m.IsActive, SyncStatus=(int)RecordSyncStatus.Pending, CreatedAtUtc=now, UpdatedAtUtc=now };
    private static void Apply(LocalSupplierEntity x, SupplierModel m, DateTime now) { x.Name=m.Name.Trim(); x.Email=m.Email.Trim(); x.Phone=m.Phone.Trim(); x.Address=m.Address.Trim(); x.TaxNumber=m.TaxNumber.Trim(); x.Notes=m.Notes.Trim(); x.IsActive=m.IsActive; x.SyncStatus=(int)RecordSyncStatus.Pending; x.SyncError=null; x.UpdatedAtUtc=now; }
    private static object Payload(LocalSupplierEntity x) => new { x.Id, x.CompanyId, x.Name, x.Email, x.Phone, x.Address, x.TaxNumber, x.IsActive };
    private static SyncStatus ToUi(int s) => (RecordSyncStatus)s == RecordSyncStatus.Synced ? SyncStatus.Synced : (RecordSyncStatus)s == RecordSyncStatus.Failed ? SyncStatus.SyncFailed : SyncStatus.PendingSync;
}
