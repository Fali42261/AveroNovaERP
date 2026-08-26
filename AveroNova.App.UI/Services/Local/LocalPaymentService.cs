using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services.Local;

public sealed class LocalPaymentService : IPaymentService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    public LocalPaymentService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<PaymentModel>> GetAllAsync(Guid companyId)
    {
        if (companyId == Guid.Empty) return [];
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Payments.AsNoTracking()
            .Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .OrderByDescending(x => x.PaymentDate).ThenByDescending(x => x.CreatedAt).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<PaymentModel?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return null;
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        return row == null ? null : Map(row);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(PaymentModel payment)
    {
        var validation = Validate(payment); if (validation != null) return (false, validation);
        await using var db = await _factory.CreateDbContextAsync(); await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            if (AffectsBalance(payment.Status))
            {
                var balanceError = await ApplyToDocumentAsync(db, payment, payment.Amount);
                if (balanceError != null) return (false, balanceError);
            }
            var now = DateTime.UtcNow;
            payment.LocalId = payment.LocalId == Guid.Empty ? Guid.NewGuid() : payment.LocalId;
            payment.SyncStatus = SyncStatus.PendingSync;
            db.Payments.Add(ToEntity(payment, now));
            await db.SaveChangesAsync(); await tx.CommitAsync(); return (true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Payment create failed: {ex}");
            return (false, "Unable to save payment locally.");
        }
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(PaymentModel payment)
    {
        var validation = Validate(payment, true); if (validation != null) return (false, validation);
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Payments.FirstOrDefaultAsync(x => x.Id == payment.LocalId && !x.IsDeleted);
        if (row == null) return (false, "Payment not found.");
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var old = Map(row);
            if (AffectsBalance(old.Status))
            {
                var reverseError = await ApplyToDocumentAsync(db, old, -old.Amount, allowOverpayCheck: false);
                if (reverseError != null) return (false, reverseError);
            }
            if (AffectsBalance(payment.Status))
            {
                var applyError = await ApplyToDocumentAsync(db, payment, payment.Amount);
                if (applyError != null) return (false, applyError);
            }

            row.Amount = payment.Amount; row.PaymentDate = payment.PaymentDate; row.Method = (int)payment.Method;
            row.PartyId = payment.PartyId; row.PartyName = payment.PartyName; row.IsSupplier = payment.IsSupplier;
            row.InvoiceId = payment.InvoiceId; row.InvoiceNumber = payment.InvoiceNumber;
            row.Reference = payment.Reference; row.Notes = payment.Notes; row.Status = (int)payment.Status;
            row.UpdatedAt = DateTime.UtcNow; row.SyncStatus = (int)SyncStatus.PendingSync;
            await db.SaveChangesAsync(); await tx.CommitAsync(); return (true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Payment update failed: {ex}");
            return (false, "Unable to update payment locally.");
        }
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        if (id == Guid.Empty) return (false, "Payment is required.");
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Payments.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (row == null) return (false, "Payment not found.");
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var payment = Map(row);
            if (AffectsBalance(payment.Status))
            {
                var error = await ApplyToDocumentAsync(db, payment, -payment.Amount, allowOverpayCheck: false);
                if (error != null) return (false, error);
            }
            row.IsDeleted = true; row.UpdatedAt = DateTime.UtcNow; row.SyncStatus = (int)SyncStatus.PendingSync;
            await db.SaveChangesAsync(); await tx.CommitAsync(); return (true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Payment delete failed: {ex}");
            return (false, "Unable to delete payment locally.");
        }
    }

    public async Task<string> GetNextPaymentNumberAsync(Guid companyId)
    {
        if (companyId == Guid.Empty) return $"PAY-{DateTime.Today:yyyy}-0001";
        await using var db = await _factory.CreateDbContextAsync();
        var prefix = $"PAY-{DateTime.Today:yyyy}-";
        var numbers = await db.Payments.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.PaymentNumber.StartsWith(prefix))
            .Select(x => x.PaymentNumber).ToListAsync();
        var max = numbers.Select(x => TrySequence(x, prefix)).DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:D4}";
    }

    private static int TrySequence(string number, string prefix)
        => number.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(number[prefix.Length..], out var value) ? value : 0;

    private static string? Validate(PaymentModel p, bool requireId = false)
    {
        if (requireId && p.LocalId == Guid.Empty) return "Payment is required.";
        if (p.CompanyId == Guid.Empty) return "Company is required.";
        if (p.Amount <= 0) return "Payment amount must be greater than zero.";
        if (!p.InvoiceId.HasValue || p.InvoiceId.Value == Guid.Empty) return "Select an invoice or purchase.";
        if (string.IsNullOrWhiteSpace(p.PartyName)) return "Customer or supplier is required.";
        return null;
    }

    private static bool AffectsBalance(PaymentStatus status) => status == PaymentStatus.Completed;

    private static async Task<string?> ApplyToDocumentAsync(AppDbContext db, PaymentModel payment, decimal delta, bool allowOverpayCheck = true)
    {
        if (!payment.InvoiceId.HasValue) return "Select an invoice or purchase.";
        var id = payment.InvoiceId.Value; var now = DateTime.UtcNow;
        if (payment.IsSupplier)
        {
            var purchase = await db.Purchases.Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == payment.CompanyId && !x.IsDeleted);
            if (purchase == null) return "Purchase not found.";
            if ((PurchaseStatus)purchase.Status == PurchaseStatus.Cancelled) return "Cancelled purchase cannot receive a payment.";
            var total = PurchaseTotal(purchase); var next = purchase.PaidAmount + delta;
            if (next < 0) next = 0;
            if (allowOverpayCheck && next > total)
                return $"Payment exceeds purchase outstanding amount ₹{Math.Max(0, total - purchase.PaidAmount):N2}.";
            purchase.PaidAmount = Math.Min(total, next); purchase.UpdatedAt = now; purchase.SyncStatus = (int)SyncStatus.PendingSync;
            return null;
        }

        var invoice = await db.Invoices.Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == payment.CompanyId && !x.IsDeleted);
        if (invoice == null) return "Invoice not found.";
        var status = (InvoiceStatus)invoice.Status;
        if (status is InvoiceStatus.Cancelled or InvoiceStatus.Draft) return "Only active sent invoices can receive payments.";
        var invoiceTotal = InvoiceTotal(invoice); var invoiceNext = invoice.PaidAmount + delta;
        if (invoiceNext < 0) invoiceNext = 0;
        if (allowOverpayCheck && invoiceNext > invoiceTotal)
            return $"Payment exceeds invoice outstanding amount ₹{Math.Max(0, invoiceTotal - invoice.PaidAmount):N2}.";
        invoice.PaidAmount = Math.Min(invoiceTotal, invoiceNext);
        invoice.Status = invoice.PaidAmount >= invoiceTotal ? (int)InvoiceStatus.Paid : invoice.PaidAmount > 0 ? (int)InvoiceStatus.PartialPaid : (int)InvoiceStatus.Sent;
        invoice.UpdatedAt = now; invoice.SyncStatus = (int)SyncStatus.PendingSync;
        return null;
    }

    private static decimal InvoiceTotal(Invoice invoice)
    {
        var items = invoice.Items.Where(x => !x.IsDeleted).ToList();
        var subtotal = items.Sum(x => x.UnitPrice * x.Quantity * (1m - x.DiscountPct / 100m));
        var lineTax = items.Sum(x => x.UnitPrice * x.Quantity * (1m - x.DiscountPct / 100m) * x.TaxPct / 100m);
        return subtotal + lineTax + subtotal * invoice.TaxPct / 100m - subtotal * invoice.DiscountPct / 100m;
    }

    private static decimal PurchaseTotal(Purchase purchase)
        => purchase.Items.Where(x => !x.IsDeleted).Sum(x => x.UnitPrice * x.Quantity * (1m + x.TaxPct / 100m));

    private static Payment ToEntity(PaymentModel x, DateTime now) => new()
    {
        Id = x.LocalId, CompanyId = x.CompanyId, PaymentNumber = x.PaymentNumber, PartyId = x.PartyId,
        PartyName = x.PartyName, IsSupplier = x.IsSupplier, InvoiceId = x.InvoiceId, InvoiceNumber = x.InvoiceNumber,
        Amount = x.Amount, PaymentDate = x.PaymentDate, Method = (int)x.Method, Reference = x.Reference,
        Notes = x.Notes, Status = (int)x.Status, SyncStatus = (int)SyncStatus.PendingSync,
        CreatedAt = now, UpdatedAt = now, IsDeleted = false
    };

    private static PaymentModel Map(Payment x) => new()
    {
        LocalId = x.Id, CompanyId = x.CompanyId, PaymentNumber = x.PaymentNumber,
        PartyId = x.PartyId, PartyName = x.PartyName, IsSupplier = x.IsSupplier,
        InvoiceId = x.InvoiceId, InvoiceNumber = x.InvoiceNumber, Amount = x.Amount,
        PaymentDate = x.PaymentDate,
        Method = Enum.IsDefined(typeof(PaymentMethod), x.Method) ? (PaymentMethod)x.Method : PaymentMethod.Cash,
        Reference = x.Reference, Notes = x.Notes,
        Status = Enum.IsDefined(typeof(PaymentStatus), x.Status) ? (PaymentStatus)x.Status : PaymentStatus.Completed,
        SyncStatus = Enum.IsDefined(typeof(SyncStatus), x.SyncStatus) ? (SyncStatus)x.SyncStatus : SyncStatus.PendingSync,
        CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt ?? x.CreatedAt, IsDeleted = x.IsDeleted
    };
}
