using System.Text.Json;
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
    private readonly IPaymentSyncService _paymentSync;
    private readonly IConnectivityService _connectivity;

    public LocalPaymentService(IDbContextFactory<LocalAppDbContext> dbFactory, IAppSessionContext session,
        IPaymentSyncService paymentSync, IConnectivityService connectivity)
    {
        _dbFactory = dbFactory; _session = session; _paymentSync = paymentSync; _connectivity = connectivity;
    }

    public async Task<List<PaymentModel>> GetAllAsync(Guid companyId)
    {
        if (!Allows(companyId)) return [];
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.Payments.AsNoTracking().Where(p => p.CompanyId == companyId)
            .OrderByDescending(p => p.PaymentDate).ToListAsync();
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
        var error = await ValidateAsync(payment);
        if (error is not null) return (false, error);

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        payment.LocalId = payment.LocalId == Guid.Empty ? Guid.NewGuid() : payment.LocalId;
        if (string.IsNullOrWhiteSpace(payment.PaymentNumber)) payment.PaymentNumber = await NextNumberAsync(db, payment.CompanyId);
        if (await db.Payments.AnyAsync(x => x.CompanyId == payment.CompanyId && x.PaymentNumber == payment.PaymentNumber))
            return (false, "Payment number already exists.");

        var row = ToEntity(payment, now);
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        db.Payments.Add(row);
        LocalSyncQueueWriter.Enqueue(db, "Payment", row.Id, row.CompanyId, SyncOperation.Create, Payload(row), now);
        await db.SaveChangesAsync();
        await RecalculateInvoiceAsync(db, row.InvoiceId, row.CompanyId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        _connectivity.IncrementPending();
        TriggerSyncIfOnline();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(PaymentModel payment)
    {
        var error = await ValidateAsync(payment);
        if (error is not null) return (false, error);

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var row = await db.Payments.FirstOrDefaultAsync(p => p.Id == payment.LocalId);
        if (row is null || !Allows(row.CompanyId)) return (false, "Payment not found.");
        var oldInvoiceId = row.InvoiceId;
        var now = DateTime.UtcNow;
        Apply(row, payment, now);
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        LocalSyncQueueWriter.Enqueue(db, "Payment", row.Id, row.CompanyId, SyncOperation.Update, Payload(row), now);
        await db.SaveChangesAsync();
        await RecalculateInvoiceAsync(db, oldInvoiceId, row.CompanyId);
        if (oldInvoiceId != row.InvoiceId) await RecalculateInvoiceAsync(db, row.InvoiceId, row.CompanyId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        _connectivity.IncrementPending();
        TriggerSyncIfOnline();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var row = await db.Payments.FirstOrDefaultAsync(p => p.Id == id);
        if (row is null || !Allows(row.CompanyId)) return (false, "Payment not found.");
        var companyId = row.CompanyId;
        var invoiceId = row.InvoiceId;
        db.Payments.Remove(row);
        LocalSyncQueueWriter.Enqueue(db, "Payment", row.Id, companyId, SyncOperation.Delete,
            new { row.Id, CompanyId = companyId, InvoiceId = invoiceId }, DateTime.UtcNow);
        await db.SaveChangesAsync();
        await RecalculateInvoiceAsync(db, invoiceId, companyId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        _connectivity.IncrementPending();
        TriggerSyncIfOnline();
        return (true, null);
    }

    public async Task<string> GetNextPaymentNumberAsync(Guid companyId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await NextNumberAsync(db, companyId);
    }

    private async Task<string?> ValidateAsync(PaymentModel payment)
    {
        if (!Allows(payment.CompanyId)) return "You do not have access to this company.";
        if (payment.Amount <= 0) return "Payment amount must be greater than zero.";
        if (payment.Method is not PaymentMethod.Cash and not PaymentMethod.Online) return "Select Cash or Online payment type.";
        if (payment.InvoiceId is Guid invoiceId && invoiceId != Guid.Empty)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == invoiceId && x.CompanyId == payment.CompanyId);
            if (invoice is null) return "Selected invoice was not found.";
            var grandTotal = CalculateGrandTotal(invoice);
            var otherPaid = await db.Payments.Where(x => x.CompanyId == payment.CompanyId && x.InvoiceId == invoiceId && x.Id != payment.LocalId && x.Status == (int)PaymentStatus.Completed)
                .SumAsync(x => (decimal?)x.Amount) ?? 0m;
            if (otherPaid + payment.Amount > grandTotal + 0.01m) return "Payment amount cannot exceed invoice due amount.";
        }
        return null;
    }

    private static async Task RecalculateInvoiceAsync(LocalAppDbContext db, Guid? invoiceId, Guid companyId)
    {
        if (invoiceId is not Guid iid || iid == Guid.Empty) return;
        var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.Id == iid && x.CompanyId == companyId);
        if (invoice is null) return;
        var paid = await db.Payments.Where(x => x.CompanyId == companyId && x.InvoiceId == iid && x.Status == (int)PaymentStatus.Completed)
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;
        invoice.PaidAmount = paid;
        if (invoice.Status != (int)InvoiceStatus.Cancelled)
        {
            var total = CalculateGrandTotal(invoice);
            invoice.Status = paid <= 0 ? invoice.Status : paid >= total - 0.01m ? (int)InvoiceStatus.Paid : (int)InvoiceStatus.PartialPaid;
        }
        invoice.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static decimal CalculateGrandTotal(LocalInvoiceEntity invoice)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<InvoiceLineItem>>(invoice.ItemsJson) ?? [];
            var subtotal = items.Sum(i => i.LineTotal);
            var tax = items.Sum(i => i.TaxAmount) + subtotal * invoice.TaxPct / 100m;
            var discount = subtotal * invoice.DiscountPct / 100m;
            return subtotal + tax - discount;
        }
        catch { return 0m; }
    }

    private void TriggerSyncIfOnline() { if (_connectivity.IsOnline) _ = _paymentSync.SyncPendingAsync(); }
    private bool Allows(Guid companyId) => _session.CurrentCompanyId is Guid current && current != Guid.Empty && current == companyId;

    private static async Task<string> NextNumberAsync(LocalAppDbContext db, Guid companyId)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"PAY-{year}-";
        var numbers = await db.Payments.AsNoTracking().Where(p => p.CompanyId == companyId && p.PaymentNumber.StartsWith(prefix))
            .Select(p => p.PaymentNumber).ToListAsync();
        var max = numbers.Select(x => int.TryParse(x[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:D4}";
    }

    private static PaymentModel Map(LocalPaymentEntity row) => new()
    {
        LocalId = row.Id, ServerId = row.ServerId?.ToString("D"), CompanyId = row.CompanyId,
        PaymentNumber = row.PaymentNumber, PartyId = row.PartyId, PartyName = row.PartyName, IsSupplier = row.IsSupplier,
        InvoiceId = row.InvoiceId, InvoiceNumber = row.InvoiceNumber, Amount = row.Amount, Method = (PaymentMethod)row.Method,
        PaymentDate = row.PaymentDate, Reference = row.Reference, Notes = row.Notes, Status = (PaymentStatus)row.Status,
        CreatedAt = row.CreatedAtUtc, UpdatedAt = row.UpdatedAtUtc, LastSyncedAt = row.LastSyncedAtUtc, SyncStatus = ToUiStatus(row.SyncStatus)
    };

    private static LocalPaymentEntity ToEntity(PaymentModel m, DateTime now) => new()
    {
        Id = m.LocalId, CompanyId = m.CompanyId, PaymentNumber = m.PaymentNumber.Trim(), PartyId = m.PartyId,
        PartyName = m.PartyName.Trim(), IsSupplier = m.IsSupplier, InvoiceId = m.InvoiceId, InvoiceNumber = m.InvoiceNumber.Trim(),
        Amount = m.Amount, Method = (int)m.Method, PaymentDate = m.PaymentDate, Reference = m.Reference.Trim(), Notes = m.Notes.Trim(),
        Status = (int)m.Status, CreatedAtUtc = now, UpdatedAtUtc = now
    };

    private static void Apply(LocalPaymentEntity row, PaymentModel m, DateTime now)
    {
        row.PaymentNumber = m.PaymentNumber.Trim(); row.PartyId = m.PartyId; row.PartyName = m.PartyName.Trim();
        row.IsSupplier = m.IsSupplier; row.InvoiceId = m.InvoiceId; row.InvoiceNumber = m.InvoiceNumber.Trim(); row.Amount = m.Amount;
        row.Method = (int)m.Method; row.PaymentDate = m.PaymentDate; row.Reference = m.Reference.Trim(); row.Notes = m.Notes.Trim();
        row.Status = (int)m.Status; row.UpdatedAtUtc = now; row.SyncError = null;
    }

    private static object Payload(LocalPaymentEntity row) => new
    {
        row.Id, row.CompanyId, row.PaymentNumber, row.PartyId, row.PartyName, row.IsSupplier, row.InvoiceId,
        row.InvoiceNumber, row.Amount, row.Method, row.PaymentDate, row.Reference, row.Notes, row.Status, SyncVersion = 1L
    };

    private static SyncStatus ToUiStatus(int status) => (RecordSyncStatus)status switch
    {
        RecordSyncStatus.Synced => SyncStatus.Synced,
        RecordSyncStatus.Failed => SyncStatus.SyncFailed,
        _ => SyncStatus.PendingSync
    };
}
