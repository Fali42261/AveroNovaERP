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
        var validationError = Validate(payment);
        if (validationError is not null)
            return (false, validationError);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var linked = await ValidateAndPrepareDocumentAsync(db, payment);
        if (linked.Error is not null) return (false, linked.Error);

        var now = DateTime.UtcNow;
        payment.LocalId = payment.LocalId == Guid.Empty ? Guid.NewGuid() : payment.LocalId;
        if (string.IsNullOrWhiteSpace(payment.PaymentNumber))
            payment.PaymentNumber = await NextNumberAsync(db, payment.CompanyId);

        var row = ToEntity(payment, now);
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        db.Payments.Add(row);
        if (linked.Invoice is not null) await ReconcileInvoiceAsync(db, linked.Invoice, row, true, now);
        if (linked.Purchase is not null) await ReconcilePurchaseAsync(db, linked.Purchase, row, true, now);
        LocalSyncQueueWriter.Enqueue(db, "Payment", row.Id, row.CompanyId, SyncOperation.Create, Payload(row), now);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(PaymentModel payment)
    {
        var validationError = Validate(payment);
        if (validationError is not null)
            return (false, validationError);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Payments.FirstOrDefaultAsync(p => p.Id == payment.LocalId);
        if (row is null || !Allows(row.CompanyId) || row.CompanyId != payment.CompanyId)
            return (false, "Payment not found.");

        var oldDocumentId = row.InvoiceId;
        var oldIsSupplier = row.IsSupplier;
        var linked = await ValidateAndPrepareDocumentAsync(db, payment, row.Id);
        if (linked.Error is not null) return (false, linked.Error);

        var now = DateTime.UtcNow;
        Apply(row, payment, now);
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        if (oldDocumentId is Guid previousId && (previousId != row.InvoiceId || oldIsSupplier != row.IsSupplier))
        {
            if (oldIsSupplier)
            {
                var previous = await db.Purchases.FirstOrDefaultAsync(x => x.Id == previousId && x.CompanyId == row.CompanyId);
                if (previous is not null) await ReconcilePurchaseAsync(db, previous, row, false, now);
            }
            else
            {
                var previous = await db.Invoices.FirstOrDefaultAsync(x => x.Id == previousId && x.CompanyId == row.CompanyId);
                if (previous is not null) await ReconcileInvoiceAsync(db, previous, row, false, now);
            }
        }
        if (linked.Invoice is not null) await ReconcileInvoiceAsync(db, linked.Invoice, row, true, now);
        if (linked.Purchase is not null) await ReconcilePurchaseAsync(db, linked.Purchase, row, true, now);
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

        var linkedInvoice = !row.IsSupplier && row.InvoiceId is Guid invoiceId
            ? await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.CompanyId == row.CompanyId) : null;
        var linkedPurchase = row.IsSupplier && row.InvoiceId is Guid purchaseId
            ? await db.Purchases.FirstOrDefaultAsync(i => i.Id == purchaseId && i.CompanyId == row.CompanyId) : null;
        db.Payments.Remove(row);
        if (linkedInvoice is not null)
            await ReconcileInvoiceAsync(db, linkedInvoice, row, includeCurrent: false, DateTime.UtcNow);
        if (linkedPurchase is not null)
            await ReconcilePurchaseAsync(db, linkedPurchase, row, includeCurrent: false, DateTime.UtcNow);
        LocalSyncQueueWriter.Enqueue(db, "Payment", row.Id, row.CompanyId, SyncOperation.Delete, new { row.Id }, DateTime.UtcNow);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<string> GetNextPaymentNumberAsync(Guid companyId)
    {
        if (!Allows(companyId))
            return string.Empty;

        await using var db = await _dbFactory.CreateDbContextAsync();
        return await NextNumberAsync(db, companyId);
    }

    private bool Allows(Guid companyId)
        => _session.CurrentCompanyId is Guid current && current != Guid.Empty && current == companyId;

    private string? Validate(PaymentModel payment)
    {
        if (!Allows(payment.CompanyId))
            return "You do not have access to this company.";
        if (string.IsNullOrWhiteSpace(payment.PartyName))
            return "Party name is required.";
        if (payment.Amount <= 0)
            return "Amount must be greater than zero.";
        if (payment.PaymentDate.Date > DateTime.Today)
            return "Payment date cannot be in the future.";
        if (!Enum.IsDefined(payment.Method))
            return "Invalid payment method.";
        if (!Enum.IsDefined(payment.Status))
            return "Invalid payment status.";
        return null;
    }

    private static async Task<(LocalInvoiceEntity? Invoice, LocalPurchaseEntity? Purchase, string? Error)> ValidateAndPrepareDocumentAsync(
        LocalAppDbContext db,
        PaymentModel payment,
        Guid? existingPaymentId = null)
    {
        if (payment.InvoiceId is not Guid invoiceId)
        {
            payment.InvoiceNumber = string.Empty;
            return (null, null, null);
        }
        if (payment.IsSupplier)
        {
            var purchase = await db.Purchases.FirstOrDefaultAsync(x => x.Id == invoiceId && x.CompanyId == payment.CompanyId);
            if (purchase is null) return (null, null, "Purchase not found.");
            if (purchase.Status is (int)PurchaseStatus.Draft or (int)PurchaseStatus.Cancelled)
                return (null, null, "Payments can only be applied to an ordered or received purchase.");
            var supplierOtherPaid = await db.Payments.Where(p => p.CompanyId == payment.CompanyId && p.InvoiceId == invoiceId
                && p.Id != existingPaymentId && p.IsSupplier && p.Status == (int)PaymentStatus.Completed).SumAsync(p => p.Amount);
            var supplierApplied = payment.Status == PaymentStatus.Completed ? payment.Amount : 0m;
            if (supplierOtherPaid + supplierApplied > LocalPurchaseService.Total(purchase))
                return (null, null, "Payment exceeds the purchase outstanding balance.");
            payment.PartyId = purchase.SupplierId;
            payment.PartyName = purchase.SupplierName;
            payment.InvoiceNumber = purchase.PurchaseNumber;
            return (null, purchase, null);
        }

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.CompanyId == payment.CompanyId);
        if (invoice is null)
            return (null, null, "Invoice not found.");
        if (invoice.Status is (int)InvoiceStatus.Draft or (int)InvoiceStatus.Cancelled)
            return (null, null, "Payments can only be applied to a posted invoice.");

        var otherPaid = await db.Payments
            .Where(p => p.CompanyId == payment.CompanyId
                        && p.InvoiceId == invoiceId
                        && p.Id != existingPaymentId
                        && !p.IsSupplier
                        && p.Status == (int)PaymentStatus.Completed)
            .SumAsync(p => p.Amount);
        var applied = payment.Status == PaymentStatus.Completed ? payment.Amount : 0m;
        if (otherPaid + applied > InvoiceTotal(invoice))
            return (null, null, "Payment exceeds the invoice outstanding balance.");

        payment.PartyId = invoice.CustomerId;
        payment.PartyName = invoice.CustomerName;
        payment.InvoiceNumber = invoice.InvoiceNumber;
        return (invoice, null, null);
    }

    private static async Task ReconcilePurchaseAsync(LocalAppDbContext db, LocalPurchaseEntity purchase,
        LocalPaymentEntity current, bool includeCurrent, DateTime now)
    {
        var paid = await db.Payments.Where(p => p.CompanyId == purchase.CompanyId && p.InvoiceId == purchase.Id
            && p.Id != current.Id && p.IsSupplier && p.Status == (int)PaymentStatus.Completed).SumAsync(p => p.Amount);
        if (includeCurrent && current.IsSupplier && current.Status == (int)PaymentStatus.Completed) paid += current.Amount;
        purchase.PaidAmount = Math.Min(LocalPurchaseService.Total(purchase), paid);
        purchase.SyncStatus = (int)RecordSyncStatus.Pending;
        purchase.SyncError = null;
        purchase.UpdatedAtUtc = now;
        LocalSyncQueueWriter.Enqueue(db, "Purchase", purchase.Id, purchase.CompanyId, SyncOperation.Update,
            LocalPurchaseService.Payload(purchase), now);
    }

    private static async Task ReconcileInvoiceAsync(
        LocalAppDbContext db,
        LocalInvoiceEntity invoice,
        LocalPaymentEntity current,
        bool includeCurrent,
        DateTime now)
    {
        var paid = await db.Payments
            .Where(p => p.CompanyId == invoice.CompanyId
                        && p.InvoiceId == invoice.Id
                        && p.Id != current.Id
                        && !p.IsSupplier
                        && p.Status == (int)PaymentStatus.Completed)
            .SumAsync(p => p.Amount);
        if (includeCurrent && current.Status == (int)PaymentStatus.Completed)
            paid += current.Amount;

        var total = InvoiceTotal(invoice);
        invoice.PaidAmount = Math.Min(total, paid);
        if (invoice.Status is not (int)InvoiceStatus.Draft and not (int)InvoiceStatus.Cancelled)
        {
            invoice.Status = invoice.PaidAmount >= total && total > 0
                ? (int)InvoiceStatus.Paid
                : invoice.PaidAmount > 0
                    ? (int)InvoiceStatus.PartialPaid
                    : invoice.DueDate.Date < DateTime.Today
                        ? (int)InvoiceStatus.Overdue
                        : (int)InvoiceStatus.Sent;
        }
        invoice.SyncStatus = (int)RecordSyncStatus.Pending;
        invoice.SyncError = null;
        invoice.UpdatedAtUtc = now;
        LocalSyncQueueWriter.Enqueue(
            db,
            "Invoice",
            invoice.Id,
            invoice.CompanyId,
            SyncOperation.Update,
            InvoicePayload(invoice),
            now);
    }

    private static decimal InvoiceTotal(LocalInvoiceEntity invoice)
    {
        var items = JsonSerializer.Deserialize<List<InvoiceLineItem>>(invoice.ItemsJson) ?? [];
        var subtotal = items.Sum(i => i.LineTotal);
        return subtotal + items.Sum(i => i.TaxAmount)
            + subtotal * invoice.TaxPct / 100
            - subtotal * invoice.DiscountPct / 100;
    }

    private static async Task<string> NextNumberAsync(LocalAppDbContext db, Guid companyId)
    {
        var prefix = $"PAY-{DateTime.UtcNow:yyyy}-";
        var numbers = await db.Payments.AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.PaymentNumber.StartsWith(prefix))
            .Select(p => p.PaymentNumber)
            .ToListAsync();
        var next = numbers
            .Select(number => int.TryParse(number[prefix.Length..], out var sequence) ? sequence : 0)
            .DefaultIfEmpty()
            .Max() + 1;
        return $"{prefix}{next:D4}";
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
        => new
        {
            row.Id, row.CompanyId, row.PaymentNumber, row.PartyId, row.PartyName,
            row.IsSupplier, row.InvoiceId, row.InvoiceNumber, row.Amount, row.Method,
            row.PaymentDate, row.Reference, row.Notes, row.Status, row.UpdatedAtUtc
        };

    private static object InvoicePayload(LocalInvoiceEntity row)
        => new
        {
            row.Id, row.CompanyId, row.InvoiceNumber, row.CustomerId, row.CustomerName,
            row.InvoiceDate, row.DueDate, row.ItemsJson, row.DiscountPct, row.TaxPct,
            row.PaymentMethod, row.Notes, row.Status, row.PaidAmount,
            GrandTotal = InvoiceTotal(row), row.UpdatedAtUtc
        };

    private static SyncStatus ToUiStatus(int status) => (RecordSyncStatus)status switch
    {
        RecordSyncStatus.Synced => SyncStatus.Synced,
        RecordSyncStatus.Failed => SyncStatus.SyncFailed,
        _ => SyncStatus.PendingSync
    };
}
