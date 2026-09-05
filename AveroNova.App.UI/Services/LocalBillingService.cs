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

        var now = DateTime.UtcNow;
        Apply(row, invoice, now);
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

        row.Status = (int)InvoiceStatus.Cancelled;
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        LocalSyncQueueWriter.Enqueue(db, "Invoice", row.Id, row.CompanyId, SyncOperation.Update, Payload(row), DateTime.UtcNow);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<string> GetNextInvoiceNumberAsync(Guid companyId)
    {
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
        var count = await db.Invoices.CountAsync(i => i.CompanyId == companyId);
        return $"INV-{DateTime.UtcNow:yyyy}-{(count + 1):D4}";
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
        row.PaidAmount = model.PaidAmount;
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

    private static object Payload(LocalInvoiceEntity row)
        => new { row.Id, row.CompanyId, row.InvoiceNumber, row.CustomerId, row.Status };

    private static SyncStatus ToUiStatus(int status) => (RecordSyncStatus)status switch
    {
        RecordSyncStatus.Synced => SyncStatus.Synced,
        RecordSyncStatus.Failed => SyncStatus.SyncFailed,
        _ => SyncStatus.PendingSync
    };
}
