using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public sealed class LocalPaymentService : IPaymentService
{
    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly IAppSessionContext _session;

    public LocalPaymentService(IDbContextFactory<LocalAppDbContext> dbFactory, IAppSessionContext session)
    {
        _dbFactory = dbFactory;
        _session = session;
    }

    public async Task<List<PaymentModel>> GetAllAsync(Guid companyId)
    {
        if (!Allows(companyId))
            return [];

        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.Payments.AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<PaymentModel?> GetByIdAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return row is null || !Allows(row.CompanyId) ? null : Map(row);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(PaymentModel payment)
    {
        if (!Allows(payment.CompanyId))
            return (false, "You do not have access to this company.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        payment.LocalId = payment.LocalId == Guid.Empty ? Guid.NewGuid() : payment.LocalId;
        if (string.IsNullOrWhiteSpace(payment.PaymentNumber))
            payment.PaymentNumber = await NextNumberAsync(db, payment.CompanyId);

        var row = ToEntity(payment, now);
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        db.Payments.Add(row);
        LocalSyncQueueWriter.Enqueue(db, "Payment", row.Id, row.CompanyId, SyncOperation.Create, Payload(row), now);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(PaymentModel payment)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Payments.FirstOrDefaultAsync(p => p.Id == payment.LocalId);
        if (row is null || !Allows(row.CompanyId))
            return (false, "Payment not found.");

        var now = DateTime.UtcNow;
        Apply(row, payment, now);
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        LocalSyncQueueWriter.Enqueue(db, "Payment", row.Id, row.CompanyId, SyncOperation.Update, Payload(row), now);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Payments.FirstOrDefaultAsync(p => p.Id == id);
        if (row is null || !Allows(row.CompanyId))
            return (false, "Payment not found.");

        db.Payments.Remove(row);
        LocalSyncQueueWriter.Enqueue(db, "Payment", row.Id, row.CompanyId, SyncOperation.Delete, new { row.Id }, DateTime.UtcNow);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<string> GetNextPaymentNumberAsync(Guid companyId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await NextNumberAsync(db, companyId);
    }

    private bool Allows(Guid companyId)
        => _session.CurrentCompanyId is Guid current && current != Guid.Empty && current == companyId;

    private static async Task<string> NextNumberAsync(LocalAppDbContext db, Guid companyId)
    {
        var count = await db.Payments.CountAsync(p => p.CompanyId == companyId);
        return $"PAY-{DateTime.UtcNow:yyyy}-{(count + 1):D4}";
    }

    private static PaymentModel Map(LocalPaymentEntity row)
        => new()
        {
            LocalId = row.Id,
            ServerId = row.ServerId?.ToString("D"),
            CompanyId = row.CompanyId,
            PaymentNumber = row.PaymentNumber,
            PartyId = row.PartyId,
            PartyName = row.PartyName,
            IsSupplier = row.IsSupplier,
            InvoiceId = row.InvoiceId,
            InvoiceNumber = row.InvoiceNumber,
            Amount = row.Amount,
            Method = (PaymentMethod)row.Method,
            PaymentDate = row.PaymentDate,
            Reference = row.Reference,
            Notes = row.Notes,
            Status = (PaymentStatus)row.Status,
            CreatedAt = row.CreatedAtUtc,
            UpdatedAt = row.UpdatedAtUtc,
            LastSyncedAt = row.LastSyncedAtUtc,
            SyncStatus = ToUiStatus(row.SyncStatus)
        };

    private static LocalPaymentEntity ToEntity(PaymentModel model, DateTime now)
        => new()
        {
            Id = model.LocalId,
            CompanyId = model.CompanyId,
            PaymentNumber = model.PaymentNumber,
            PartyId = model.PartyId,
            PartyName = model.PartyName,
            IsSupplier = model.IsSupplier,
            InvoiceId = model.InvoiceId,
            InvoiceNumber = model.InvoiceNumber,
            Amount = model.Amount,
            Method = (int)model.Method,
            PaymentDate = model.PaymentDate,
            Reference = model.Reference,
            Notes = model.Notes,
            Status = (int)model.Status,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    private static void Apply(LocalPaymentEntity row, PaymentModel model, DateTime now)
    {
        row.PaymentNumber = model.PaymentNumber;
        row.PartyId = model.PartyId;
        row.PartyName = model.PartyName;
        row.IsSupplier = model.IsSupplier;
        row.InvoiceId = model.InvoiceId;
        row.InvoiceNumber = model.InvoiceNumber;
        row.Amount = model.Amount;
        row.Method = (int)model.Method;
        row.PaymentDate = model.PaymentDate;
        row.Reference = model.Reference;
        row.Notes = model.Notes;
        row.Status = (int)model.Status;
        row.UpdatedAtUtc = now;
        row.SyncError = null;
    }

    private static object Payload(LocalPaymentEntity row)
        => new { row.Id, row.CompanyId, row.PaymentNumber, row.Amount, row.InvoiceId };

    private static SyncStatus ToUiStatus(int status) => (RecordSyncStatus)status switch
    {
        RecordSyncStatus.Synced => SyncStatus.Synced,
        RecordSyncStatus.Failed => SyncStatus.SyncFailed,
        _ => SyncStatus.PendingSync
    };
}
