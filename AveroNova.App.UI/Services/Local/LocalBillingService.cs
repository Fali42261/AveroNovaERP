using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services.Local;

/// <summary>
/// Offline-first billing service. All invoice reads/writes use the local SQLite database.
/// Server synchronization is intentionally handled separately through SyncStatus/PendingSync.
/// </summary>
public sealed class LocalBillingService : IBillingService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public LocalBillingService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<InvoiceModel>> GetAllAsync(Guid companyId)
    {
        if (companyId == Guid.Empty) return [];
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Invoices.AsNoTracking().Include(x => x.Items)
            .Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.CreatedAt).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<InvoiceModel?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return null;
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Invoices.AsNoTracking().Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        return row == null ? null : Map(row);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(InvoiceModel invoice)
    {
        if (invoice.CompanyId == Guid.Empty) return (false, "Company is required.");
        if (invoice.CustomerId == Guid.Empty) return (false, "Customer is required.");
        if (invoice.Items.Count == 0) return (false, "Add at least one line item.");
        if (invoice.Items.Any(x => x.ProductId == Guid.Empty || x.Quantity <= 0 || x.UnitPrice < 0)) return (false, "Invoice contains an invalid line item.");
        if (invoice.PaidAmount < 0 || invoice.PaidAmount > invoice.GrandTotal) return (false, "Paid amount is invalid.");

        await using var db = await _factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            invoice.LocalId = invoice.LocalId == Guid.Empty ? Guid.NewGuid() : invoice.LocalId;
            invoice.CreatedAt = now; invoice.UpdatedAt = now; invoice.SyncStatus = SyncStatus.PendingSync;
            var entity = ToEntity(invoice, now);
            db.Invoices.Add(entity);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            invoice.LocalId = entity.Id;
            return (true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Offline invoice create failed: {ex}");
            return (false, "Unable to save invoice locally.");
        }
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(InvoiceModel invoice)
    {
        if (invoice.LocalId == Guid.Empty) return (false, "Invoice is required.");
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await db.Invoices.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == invoice.LocalId && !x.IsDeleted);
        if (entity == null) return (false, "Invoice not found.");
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            entity.CustomerId = invoice.CustomerId; entity.CustomerName = invoice.CustomerName;
            entity.InvoiceDate = invoice.InvoiceDate; entity.DueDate = invoice.DueDate;
            entity.DiscountPct = invoice.DiscountPct; entity.TaxPct = invoice.TaxPct;
            entity.PaymentMethod = (int)invoice.PaymentMethod; entity.PaidAmount = invoice.PaidAmount;
            entity.Notes = invoice.Notes; entity.Status = (int)invoice.Status;
            entity.UpdatedAt = DateTime.UtcNow; entity.SyncStatus = (int)SyncStatus.PendingSync;
            foreach (var old in entity.Items) old.IsDeleted = true;
            entity.Items = invoice.Items.Select(x => new InvoiceItem
            {
                Id = Guid.NewGuid(), InvoiceId = entity.Id, ProductId = x.ProductId, ProductName = x.ProductName,
                SKU = x.SKU, UnitPrice = x.UnitPrice, Quantity = x.Quantity, DiscountPct = x.DiscountPct,
                TaxPct = x.TaxPct, CreatedAt = DateTime.UtcNow, IsDeleted = false
            }).ToList();
            await db.SaveChangesAsync(); await tx.CommitAsync();
            invoice.SyncStatus = SyncStatus.PendingSync;
            return (true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Offline invoice update failed: {ex}");
            return (false, "Unable to update invoice locally.");
        }
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        if (id == Guid.Empty) return (false, "Invoice is required.");
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await db.Invoices.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (entity == null) return (false, "Invoice not found.");
        entity.IsDeleted = true; entity.UpdatedAt = DateTime.UtcNow; entity.SyncStatus = (int)SyncStatus.PendingSync;
        await db.SaveChangesAsync(); return (true, null);
    }

    public async Task<(bool Ok, string? Error)> CancelAsync(Guid id)
    {
        if (id == Guid.Empty) return (false, "Invoice is required.");
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await db.Invoices.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (entity == null) return (false, "Invoice not found.");
        entity.Status = (int)InvoiceStatus.Cancelled; entity.UpdatedAt = DateTime.UtcNow; entity.SyncStatus = (int)SyncStatus.PendingSync;
        await db.SaveChangesAsync(); return (true, null);
    }

    public async Task<string> GetNextInvoiceNumberAsync(Guid companyId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var count = await db.Invoices.CountAsync(x => x.CompanyId == companyId && !x.IsDeleted);
        return $"INV-{DateTime.Today:yyyy}-{count + 1:D4}";
    }

    public async Task<List<InvoiceModel>> GetByCustomerAsync(Guid customerId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Invoices.AsNoTracking().Include(x => x.Items).Where(x => x.CustomerId == customerId && !x.IsDeleted).OrderByDescending(x => x.InvoiceDate).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<List<InvoiceModel>> GetOverdueAsync(Guid companyId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var today = DateTime.Today;
        var rows = await db.Invoices.AsNoTracking().Include(x => x.Items)
            .Where(x => x.CompanyId == companyId && !x.IsDeleted && x.DueDate < today && x.Status != (int)InvoiceStatus.Paid && x.Status != (int)InvoiceStatus.Cancelled)
            .OrderBy(x => x.DueDate).ToListAsync();
        return rows.Select(Map).ToList();
    }

    private static Invoice ToEntity(InvoiceModel model, DateTime now) => new()
    {
        Id = model.LocalId, CompanyId = model.CompanyId, InvoiceNumber = model.InvoiceNumber,
        CustomerId = model.CustomerId, CustomerName = model.CustomerName, InvoiceDate = model.InvoiceDate,
        DueDate = model.DueDate, DiscountPct = model.DiscountPct, TaxPct = model.TaxPct,
        PaymentMethod = (int)model.PaymentMethod, PaidAmount = model.PaidAmount, Notes = model.Notes,
        Status = (int)model.Status, SyncStatus = (int)SyncStatus.PendingSync, CreatedAt = now, UpdatedAt = now, IsDeleted = false,
        Items = model.Items.Select(x => new InvoiceItem
        {
            Id = Guid.NewGuid(), InvoiceId = model.LocalId, ProductId = x.ProductId, ProductName = x.ProductName,
            SKU = x.SKU, UnitPrice = x.UnitPrice, Quantity = x.Quantity, DiscountPct = x.DiscountPct,
            TaxPct = x.TaxPct, CreatedAt = now, UpdatedAt = now, IsDeleted = false
        }).ToList()
    };

    private static InvoiceModel Map(Invoice entity) => new()
    {
        LocalId = entity.Id, CompanyId = entity.CompanyId, InvoiceNumber = entity.InvoiceNumber,
        CustomerId = entity.CustomerId, CustomerName = entity.CustomerName, InvoiceDate = entity.InvoiceDate,
        DueDate = entity.DueDate, DiscountPct = entity.DiscountPct, TaxPct = entity.TaxPct,
        PaymentMethod = Enum.IsDefined(typeof(PaymentMethod), entity.PaymentMethod) ? (PaymentMethod)entity.PaymentMethod : PaymentMethod.Cash,
        PaidAmount = entity.PaidAmount, Notes = entity.Notes,
        Status = Enum.IsDefined(typeof(InvoiceStatus), entity.Status) ? (InvoiceStatus)entity.Status : InvoiceStatus.Draft,
        SyncStatus = Enum.IsDefined(typeof(SyncStatus), entity.SyncStatus) ? (SyncStatus)entity.SyncStatus : SyncStatus.PendingSync,
        CreatedAt = entity.CreatedAt, UpdatedAt = entity.UpdatedAt ?? entity.CreatedAt, IsDeleted = entity.IsDeleted,
        Items = entity.Items.Where(x => !x.IsDeleted).Select(x => new InvoiceLineItem
        {
            ProductId = x.ProductId, ProductName = x.ProductName, SKU = x.SKU, UnitPrice = x.UnitPrice,
            Quantity = x.Quantity, DiscountPct = x.DiscountPct, TaxPct = x.TaxPct
        }).ToList()
    };
}
