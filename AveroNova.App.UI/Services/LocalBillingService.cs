using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Api;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Security;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AveroNova.App.UI.Services;

public sealed class LocalBillingService : IBillingService
{
    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly IAppSessionContext _session;
    private readonly InvoiceSyncService _invoiceSync;
    private readonly IConnectivityService _connectivity;

    public LocalBillingService(IDbContextFactory<LocalAppDbContext> dbFactory, IAppSessionContext session,
        IConnectivityService connectivity, IApiClient api, ISecureTokenStore tokens, ILogger<InvoiceSyncService> logger)
    {
        _dbFactory = dbFactory;
        _session = session;
        _connectivity = connectivity;
        _invoiceSync = new InvoiceSyncService(dbFactory, api, tokens, connectivity, logger);
    }

    public async Task<List<InvoiceModel>> GetAllAsync(Guid companyId)
    {
        if (!Allows(companyId)) return [];
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.Invoices.AsNoTracking().Where(i => i.CompanyId == companyId)
            .OrderByDescending(i => i.InvoiceDate).ToListAsync();
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
        if (!Allows(invoice.CompanyId)) return (false, "You do not have access to this company.");
        if (invoice.CustomerId == Guid.Empty) return (false, "Select a customer.");
        if (invoice.Items.Count == 0) return (false, "Add at least one line item.");
        if (invoice.Items.Any(x => x.ProductId == Guid.Empty || x.Quantity <= 0 || x.UnitPrice < 0))
            return (false, "Invoice contains an invalid line item.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        invoice.LocalId = invoice.LocalId == Guid.Empty ? Guid.NewGuid() : invoice.LocalId;
        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            invoice.InvoiceNumber = await NextNumberAsync(db, invoice.CompanyId);

        var duplicate = await db.Invoices.AnyAsync(x => x.CompanyId == invoice.CompanyId && x.InvoiceNumber == invoice.InvoiceNumber);
        if (duplicate) return (false, "Invoice number already exists.");

        var row = ToEntity(invoice, now);
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        db.Invoices.Add(row);
        LocalSyncQueueWriter.Enqueue(db, "Invoice", row.Id, row.CompanyId, SyncOperation.Create, Payload(row), now);
        await db.SaveChangesAsync();
        _connectivity.IncrementPending();
        TriggerSyncIfOnline();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(InvoiceModel invoice)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoice.LocalId);
        if (row is null || !Allows(row.CompanyId)) return (false, "Invoice not found.");
        if (invoice.CustomerId == Guid.Empty || invoice.Items.Count == 0)
            return (false, "Customer and at least one line item are required.");

        var now = DateTime.UtcNow;
        Apply(row, invoice, now);
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        LocalSyncQueueWriter.Enqueue(db, "Invoice", row.Id, row.CompanyId, SyncOperation.Update, Payload(row), now);
        await db.SaveChangesAsync();
        _connectivity.IncrementPending();
        TriggerSyncIfOnline();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id);
        if (row is null || !Allows(row.CompanyId)) return (false, "Invoice not found.");

        var companyId = row.CompanyId;
        db.Invoices.Remove(row);
        LocalSyncQueueWriter.Enqueue(db, "Invoice", row.Id, companyId, SyncOperation.Delete,
            new { row.Id, CompanyId = companyId }, DateTime.UtcNow);
        await db.SaveChangesAsync();
        _connectivity.IncrementPending();
        TriggerSyncIfOnline();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> CancelAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id);
        if (row is null || !Allows(row.CompanyId)) return (false, "Invoice not found.");
        if (row.Status == (int)InvoiceStatus.Cancelled) return (true, null);

        var now = DateTime.UtcNow;
        row.Status = (int)InvoiceStatus.Cancelled;
        row.UpdatedAtUtc = now;
        row.SyncStatus = (int)RecordSyncStatus.Pending;
        row.SyncError = null;
        LocalSyncQueueWriter.Enqueue(db, "Invoice", row.Id, row.CompanyId, SyncOperation.Update, Payload(row), now);
        await db.SaveChangesAsync();
        _connectivity.IncrementPending();
        TriggerSyncIfOnline();
        return (true, null);
    }

    public async Task<string> GetNextInvoiceNumberAsync(Guid companyId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await NextNumberAsync(db, companyId);
    }

    public async Task<List<InvoiceModel>> GetByCustomerAsync(Guid customerId)
    {
        if (_session.CurrentCompanyId is not Guid companyId) return [];
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.Invoices.AsNoTracking().Where(i => i.CompanyId == companyId && i.CustomerId == customerId).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<List<InvoiceModel>> GetOverdueAsync(Guid companyId)
        => (await GetAllAsync(companyId)).Where(i => i.Status == InvoiceStatus.Overdue ||
            (i.Status is not InvoiceStatus.Paid and not InvoiceStatus.Cancelled && i.DueDate.Date < DateTime.Today)).ToList();

    private void TriggerSyncIfOnline()
    {
        if (_connectivity.IsOnline) _ = _invoiceSync.SyncPendingAsync();
    }

    private bool Allows(Guid companyId)
        => _session.CurrentCompanyId is Guid current && current != Guid.Empty && current == companyId;

    private static async Task<string> NextNumberAsync(LocalAppDbContext db, Guid companyId)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"INV-{year}-";
        var numbers = await db.Invoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.InvoiceNumber.StartsWith(prefix))
            .Select(i => i.InvoiceNumber).ToListAsync();
        var max = numbers.Select(x => int.TryParse(x[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:D4}";
    }

    private static InvoiceModel Map(LocalInvoiceEntity row)
    {
        var items = DeserializeItems(row.ItemsJson);
        var status = (InvoiceStatus)row.Status;
        if (status is not InvoiceStatus.Paid and not InvoiceStatus.Cancelled && row.DueDate.Date < DateTime.Today)
            status = InvoiceStatus.Overdue;
        return new InvoiceModel
        {
            LocalId = row.Id, ServerId = row.ServerId?.ToString("D"), CompanyId = row.CompanyId,
            InvoiceNumber = row.InvoiceNumber, CustomerId = row.CustomerId, CustomerName = row.CustomerName,
            InvoiceDate = row.InvoiceDate, DueDate = row.DueDate, Items = items, DiscountPct = row.DiscountPct,
            TaxPct = row.TaxPct, PaymentMethod = (PaymentMethod)row.PaymentMethod, Notes = row.Notes,
            Status = status, PaidAmount = row.PaidAmount, CreatedAt = row.CreatedAtUtc, UpdatedAt = row.UpdatedAtUtc,
            LastSyncedAt = row.LastSyncedAtUtc, SyncStatus = ToUiStatus(row.SyncStatus)
        };
    }

    private static LocalInvoiceEntity ToEntity(InvoiceModel model, DateTime now) => new()
    {
        Id = model.LocalId, CompanyId = model.CompanyId, InvoiceNumber = model.InvoiceNumber.Trim(),
        CustomerId = model.CustomerId, CustomerName = model.CustomerName.Trim(), InvoiceDate = model.InvoiceDate,
        DueDate = model.DueDate, ItemsJson = JsonSerializer.Serialize(model.Items), DiscountPct = model.DiscountPct,
        TaxPct = model.TaxPct, PaymentMethod = (int)model.PaymentMethod, Notes = model.Notes,
        Status = (int)model.Status, PaidAmount = model.PaidAmount, CreatedAtUtc = now, UpdatedAtUtc = now
    };

    private static void Apply(LocalInvoiceEntity row, InvoiceModel model, DateTime now)
    {
        row.InvoiceNumber = model.InvoiceNumber.Trim(); row.CustomerId = model.CustomerId;
        row.CustomerName = model.CustomerName.Trim(); row.InvoiceDate = model.InvoiceDate; row.DueDate = model.DueDate;
        row.ItemsJson = JsonSerializer.Serialize(model.Items); row.DiscountPct = model.DiscountPct; row.TaxPct = model.TaxPct;
        row.PaymentMethod = (int)model.PaymentMethod; row.Notes = model.Notes; row.Status = (int)model.Status;
        row.PaidAmount = model.PaidAmount; row.UpdatedAtUtc = now; row.SyncError = null;
    }

    private static List<InvoiceLineItem> DeserializeItems(string json)
    {
        try { return JsonSerializer.Deserialize<List<InvoiceLineItem>>(json) ?? []; }
        catch { return []; }
    }

    private static object Payload(LocalInvoiceEntity row) => new
    {
        row.Id, row.CompanyId, row.InvoiceNumber, row.CustomerId, row.CustomerName,
        row.InvoiceDate, row.DueDate, row.ItemsJson, row.DiscountPct, row.TaxPct,
        row.PaymentMethod, row.Notes, row.Status, row.PaidAmount, SyncVersion = 1L
    };

    private static SyncStatus ToUiStatus(int status) => (RecordSyncStatus)status switch
    {
        RecordSyncStatus.Synced => SyncStatus.Synced,
        RecordSyncStatus.Failed => SyncStatus.SyncFailed,
        _ => SyncStatus.PendingSync
    };
}
