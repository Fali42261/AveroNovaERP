using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public sealed class LocalBillingService : IBillingService
{
    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly IAppSessionContext _session;

    public LocalBillingService(IDbContextFactory<LocalAppDbContext> dbFactory, IAppSessionContext session)
    {
        _dbFactory = dbFactory;
        _session = session;
    }

    public async Task<List<InvoiceModel>> GetAllAsync(Guid companyId)
    {
        if (!Allows(companyId))
            return [];

        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.Invoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<InvoiceModel?> GetByIdAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        return row is null || !Allows(row.CompanyId) ? null : Map(row);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(InvoiceModel invoice)
    {
        if (!Allows(invoice.CompanyId))
            return (false, "You do not have access to this company.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        invoice.LocalId = invoice.LocalId == Guid.Empty ? Guid.NewGuid() : invoice.LocalId;
        invoice.PaidAmount = 0;
        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            invoice.InvoiceNumber = await NextNumberAsync(db, invoice.CompanyId);

        var row = ToEntity(invoice, now);
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        db.Invoices.Add(row);
        LocalSyncQueueWriter.Enqueue(db, "Invoice", row.Id, row.CompanyId, SyncOperation.Create, Payload(row), now);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(InvoiceModel invoice)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoice.LocalId);
        if (row is null || !Allows(row.CompanyId))
            return (false, "Invoice not found.");

        var paid = await db.Payments
            .Where(p => p.CompanyId == row.CompanyId && p.InvoiceId == row.Id && p.Status == (int)PaymentStatus.Completed)
            .SumAsync(p => p.Amount);
        if (paid > invoice.GrandTotal)
            return (false, "Invoice total cannot be less than payments already applied.");

        var now = DateTime.UtcNow;
        Apply(row, invoice, now);
        row.PaidAmount = paid;
        ApplyReconciledStatus(row, invoice.GrandTotal);
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        LocalSyncQueueWriter.Enqueue(db, "Invoice", row.Id, row.CompanyId, SyncOperation.Update, Payload(row), now);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id);
        if (row is null || !Allows(row.CompanyId))
            return (false, "Invoice not found.");

        if (await db.Payments.AnyAsync(p => p.CompanyId == row.CompanyId && p.InvoiceId == row.Id))
            return (false, "Delete linked payments before deleting this invoice.");

        db.Invoices.Remove(row);
        LocalSyncQueueWriter.Enqueue(db, "Invoice", row.Id, row.CompanyId, SyncOperation.Delete, new { row.Id }, DateTime.UtcNow);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> CancelAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id);
        if (row is null || !Allows(row.CompanyId))
            return (false, "Invoice not found.");

        if (row.PaidAmount > 0)
            return (false, "An invoice with applied payments cannot be cancelled.");

        row.Status = (int)InvoiceStatus.Cancelled;
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        LocalSyncQueueWriter.Enqueue(db, "Invoice", row.Id, row.CompanyId, SyncOperation.Update, Payload(row), DateTime.UtcNow);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<string> GetNextInvoiceNumberAsync(Guid companyId)
    {
        if (!Allows(companyId))
            return string.Empty;

        await using var db = await _dbFactory.CreateDbContextAsync();
        return await NextNumberAsync(db, companyId);
    }

    public async Task<List<InvoiceModel>> GetByCustomerAsync(Guid customerId)
    {
        if (_session.CurrentCompanyId is not Guid companyId)
            return [];

        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.Invoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.CustomerId == customerId)
            .ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<List<InvoiceModel>> GetOverdueAsync(Guid companyId)
        => (await GetAllAsync(companyId)).Where(i => i.Status == InvoiceStatus.Overdue).ToList();

    private bool Allows(Guid companyId)
        => _session.CurrentCompanyId is Guid current && current != Guid.Empty && current == companyId;

    private static async Task<string> NextNumberAsync(LocalAppDbContext db, Guid companyId)
    {
        var prefix = $"INV-{DateTime.UtcNow:yyyy}-";
        var numbers = await db.Invoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.InvoiceNumber.StartsWith(prefix))
            .Select(i => i.InvoiceNumber)
            .ToListAsync();
        var next = numbers
            .Select(number => int.TryParse(number[prefix.Length..], out var sequence) ? sequence : 0)
            .DefaultIfEmpty()
            .Max() + 1;
        return $"{prefix}{next:D4}";
    }

    private static InvoiceModel Map(LocalInvoiceEntity row)
    {
        var items = DeserializeItems(row.ItemsJson);
        return new InvoiceModel
        {
            LocalId = row.Id,
            ServerId = row.ServerId?.ToString("D"),
            CompanyId = row.CompanyId,
            InvoiceNumber = row.InvoiceNumber,
            CustomerId = row.CustomerId,
            CustomerName = row.CustomerName,
            InvoiceDate = row.InvoiceDate,
            DueDate = row.DueDate,
            Items = items,
            DiscountPct = row.DiscountPct,
            TaxPct = row.TaxPct,
            PaymentMethod = (PaymentMethod)row.PaymentMethod,
            Notes = row.Notes,
            Status = (InvoiceStatus)row.Status,
            PaidAmount = row.PaidAmount,
            CreatedAt = row.CreatedAtUtc,
            UpdatedAt = row.UpdatedAtUtc,
            LastSyncedAt = row.LastSyncedAtUtc,
            SyncStatus = ToUiStatus(row.SyncStatus)
        };
    }

    private static LocalInvoiceEntity ToEntity(InvoiceModel model, DateTime now)
        => new()
        {
            Id = model.LocalId,
            CompanyId = model.CompanyId,
            InvoiceNumber = model.InvoiceNumber,
            CustomerId = model.CustomerId,
            CustomerName = model.CustomerName,
            InvoiceDate = model.InvoiceDate,
            DueDate = model.DueDate,
            ItemsJson = JsonSerializer.Serialize(model.Items),
            DiscountPct = model.DiscountPct,
            TaxPct = model.TaxPct,
            PaymentMethod = (int)model.PaymentMethod,
            Notes = model.Notes,
            Status = (int)model.Status,
            PaidAmount = model.PaidAmount,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    private static void Apply(LocalInvoiceEntity row, InvoiceModel model, DateTime now)
    {
        row.InvoiceNumber = model.InvoiceNumber;
        row.CustomerId = model.CustomerId;
        row.CustomerName = model.CustomerName;
        row.InvoiceDate = model.InvoiceDate;
        row.DueDate = model.DueDate;
        row.ItemsJson = JsonSerializer.Serialize(model.Items);
        row.DiscountPct = model.DiscountPct;
        row.TaxPct = model.TaxPct;
        row.PaymentMethod = (int)model.PaymentMethod;
        row.Notes = model.Notes;
        row.Status = (int)model.Status;
        row.UpdatedAtUtc = now;
        row.SyncError = null;
    }

    private static List<InvoiceLineItem> DeserializeItems(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<InvoiceLineItem>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void ApplyReconciledStatus(LocalInvoiceEntity row, decimal total)
    {
        if (row.Status == (int)InvoiceStatus.Cancelled)
            return;
        row.Status = row.PaidAmount >= total && total > 0
            ? (int)InvoiceStatus.Paid
            : row.PaidAmount > 0
                ? (int)InvoiceStatus.PartialPaid
                : row.Status;
    }

    private static object Payload(LocalInvoiceEntity row)
        => new
        {
            row.Id, row.CompanyId, row.InvoiceNumber, row.CustomerId, row.CustomerName,
            row.InvoiceDate, row.DueDate, row.ItemsJson, row.DiscountPct, row.TaxPct,
            row.PaymentMethod, row.Notes, row.Status, row.PaidAmount,
            GrandTotal = InvoiceTotal(row), row.UpdatedAtUtc
        };

    private static decimal InvoiceTotal(LocalInvoiceEntity row)
    {
        var items = DeserializeItems(row.ItemsJson);
        var subtotal = items.Sum(i => i.LineTotal);
        return subtotal + items.Sum(i => i.TaxAmount)
            + subtotal * row.TaxPct / 100
            - subtotal * row.DiscountPct / 100;
    }

    private static SyncStatus ToUiStatus(int status) => (RecordSyncStatus)status switch
    {
        RecordSyncStatus.Synced => SyncStatus.Synced,
        RecordSyncStatus.Failed => SyncStatus.SyncFailed,
        _ => SyncStatus.PendingSync
    };
}
