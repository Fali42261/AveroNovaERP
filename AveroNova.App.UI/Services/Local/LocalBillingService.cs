using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services.Local;

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
        var validation = Validate(invoice); if (validation != null) return (false, validation);
        await using var db = await _factory.CreateDbContextAsync(); await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow; var required = Quantities(invoice.Items);
            var products = await LoadProductsAsync(db, invoice.CompanyId, required.Keys);
            if (products.Count != required.Count) return (false, "One or more products were not found.");
            if (AffectsStock(invoice.Status))
                foreach (var product in products)
                    if (product.Stock < required[product.Id]) return (false, $"Insufficient stock for {product.Name}. Available: {product.Stock}.");

            invoice.LocalId = invoice.LocalId == Guid.Empty ? Guid.NewGuid() : invoice.LocalId;
            invoice.CreatedAt = now; invoice.UpdatedAt = now; invoice.SyncStatus = SyncStatus.PendingSync;
            var entity = ToEntity(invoice, now); db.Invoices.Add(entity);
            if (AffectsStock(invoice.Status))
                foreach (var product in products)
                    ApplyStockChange(db, invoice.CompanyId, product, -required[product.Id], StockMovementType.Out, invoice.InvoiceNumber, "Sale", now);

            await db.SaveChangesAsync(); await tx.CommitAsync(); invoice.LocalId = entity.Id; return (true, null);
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
        var validation = Validate(invoice, requireId: true); if (validation != null) return (false, validation);
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await db.Invoices.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == invoice.LocalId && !x.IsDeleted);
        if (entity == null) return (false, "Invoice not found.");
        if ((InvoiceStatus)entity.Status == InvoiceStatus.Cancelled) return (false, "Cancelled invoices cannot be edited.");
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            var oldItems = entity.Items.Where(x => !x.IsDeleted).Select(x => new InvoiceLineItem { ProductId = x.ProductId, Quantity = x.Quantity }).ToList();
            var oldQty = AffectsStock((InvoiceStatus)entity.Status) ? Quantities(oldItems) : new Dictionary<Guid, int>();
            var newQty = AffectsStock(invoice.Status) ? Quantities(invoice.Items) : new Dictionary<Guid, int>();
            var ids = oldQty.Keys.Union(newQty.Keys).ToList();
            var products = await LoadProductsAsync(db, entity.CompanyId, ids);
            if (products.Count != ids.Count) return (false, "One or more products were not found.");

            foreach (var product in products)
            {
                var oldValue = oldQty.GetValueOrDefault(product.Id); var newValue = newQty.GetValueOrDefault(product.Id); var delta = newValue - oldValue;
                if (delta > 0 && product.Stock < delta) return (false, $"Insufficient stock for {product.Name}. Available: {product.Stock}.");
            }
            foreach (var product in products)
            {
                var delta = newQty.GetValueOrDefault(product.Id) - oldQty.GetValueOrDefault(product.Id);
                if (delta == 0) continue;
                ApplyStockChange(db, entity.CompanyId, product, -delta, delta > 0 ? StockMovementType.Out : StockMovementType.Return, invoice.InvoiceNumber, "Sale edited", now);
            }

            entity.CustomerId = invoice.CustomerId; entity.CustomerName = invoice.CustomerName; entity.InvoiceDate = invoice.InvoiceDate; entity.DueDate = invoice.DueDate;
            entity.DiscountPct = invoice.DiscountPct; entity.TaxPct = invoice.TaxPct; entity.PaymentMethod = (int)invoice.PaymentMethod; entity.PaidAmount = invoice.PaidAmount;
            entity.Notes = invoice.Notes; entity.Status = (int)invoice.Status; entity.UpdatedAt = now; entity.SyncStatus = (int)SyncStatus.PendingSync;
            foreach (var old in entity.Items) old.IsDeleted = true;
            entity.Items = invoice.Items.Select(x => new InvoiceItem
            {
                Id = Guid.NewGuid(), InvoiceId = entity.Id, ProductId = x.ProductId, ProductName = x.ProductName, SKU = x.SKU,
                UnitPrice = x.UnitPrice, Quantity = x.Quantity, DiscountPct = x.DiscountPct, TaxPct = x.TaxPct,
                CreatedAt = now, UpdatedAt = now, IsDeleted = false
            }).ToList();
            await db.SaveChangesAsync(); await tx.CommitAsync(); invoice.SyncStatus = SyncStatus.PendingSync; return (true, null);
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
        var entity = await db.Invoices.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (entity == null) return (false, "Invoice not found.");
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            if (AffectsStock((InvoiceStatus)entity.Status)) await RestoreInvoiceStockAsync(db, entity, "Sale deleted", now);
            entity.IsDeleted = true; entity.UpdatedAt = now; entity.SyncStatus = (int)SyncStatus.PendingSync;
            await db.SaveChangesAsync(); await tx.CommitAsync(); return (true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Offline invoice delete failed: {ex}");
            return (false, "Unable to delete invoice locally.");
        }
    }

    public async Task<(bool Ok, string? Error)> CancelAsync(Guid id)
    {
        if (id == Guid.Empty) return (false, "Invoice is required.");
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await db.Invoices.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (entity == null) return (false, "Invoice not found.");
        if ((InvoiceStatus)entity.Status == InvoiceStatus.Cancelled) return (true, null);
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            if (AffectsStock((InvoiceStatus)entity.Status)) await RestoreInvoiceStockAsync(db, entity, "Sale cancelled", now);
            entity.Status = (int)InvoiceStatus.Cancelled; entity.UpdatedAt = now; entity.SyncStatus = (int)SyncStatus.PendingSync;
            await db.SaveChangesAsync(); await tx.CommitAsync(); return (true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Offline invoice cancel failed: {ex}");
            return (false, "Unable to cancel invoice locally.");
        }
    }

    public async Task<string> GetNextInvoiceNumberAsync(Guid companyId)
    {
        if (companyId == Guid.Empty) return $"INV-{DateTime.Today:yyyy}-0001";
        await using var db = await _factory.CreateDbContextAsync();
        var prefix = $"INV-{DateTime.Today:yyyy}-";
        var numbers = await db.Invoices.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.InvoiceNumber.StartsWith(prefix))
            .Select(x => x.InvoiceNumber).ToListAsync();
        var max = numbers.Select(x => TrySequence(x, prefix)).DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:D4}";
    }

    public async Task<List<InvoiceModel>> GetByCustomerAsync(Guid customerId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Invoices.AsNoTracking().Include(x => x.Items)
            .Where(x => x.CustomerId == customerId && !x.IsDeleted)
            .OrderByDescending(x => x.InvoiceDate).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<List<InvoiceModel>> GetOverdueAsync(Guid companyId)
    {
        await using var db = await _factory.CreateDbContextAsync(); var today = DateTime.Today;
        var rows = await db.Invoices.AsNoTracking().Include(x => x.Items)
            .Where(x => x.CompanyId == companyId && !x.IsDeleted && x.DueDate < today && x.Status != (int)InvoiceStatus.Paid && x.Status != (int)InvoiceStatus.Cancelled && x.Status != (int)InvoiceStatus.Draft)
            .OrderBy(x => x.DueDate).ToListAsync();
        return rows.Select(Map).ToList();
    }

    private static int TrySequence(string number, string prefix)
        => number.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(number[prefix.Length..], out var value) ? value : 0;

    private static string? Validate(InvoiceModel invoice, bool requireId = false)
    {
        if (requireId && invoice.LocalId == Guid.Empty) return "Invoice is required.";
        if (invoice.CompanyId == Guid.Empty) return "Company is required.";
        if (invoice.CustomerId == Guid.Empty) return "Customer is required.";
        if (invoice.Items.Count == 0) return "Add at least one line item.";
        if (invoice.Items.Any(x => x.ProductId == Guid.Empty || x.Quantity <= 0 || x.UnitPrice < 0)) return "Invoice contains an invalid line item.";
        if (invoice.PaidAmount < 0 || invoice.PaidAmount > invoice.GrandTotal) return "Paid amount is invalid.";
        return null;
    }

    private static bool AffectsStock(InvoiceStatus status) => status != InvoiceStatus.Draft && status != InvoiceStatus.Cancelled;
    private static Dictionary<Guid, int> Quantities(IEnumerable<InvoiceLineItem> items) => items.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

    private static async Task<List<Product>> LoadProductsAsync(AppDbContext db, Guid companyId, IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList(); if (list.Count == 0) return [];
        return await db.Products.Where(p => p.CompanyId == companyId && !p.IsDeleted && list.Contains(p.Id)).ToListAsync();
    }

    private static void ApplyStockChange(AppDbContext db, Guid companyId, Product product, int signedChange, StockMovementType type, string reference, string notes, DateTime now)
    {
        var before = product.Stock; product.Stock += signedChange; product.UpdatedAt = now;
        db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(), CompanyId = companyId, ProductId = product.Id, MovementType = (int)type, Quantity = signedChange,
            StockBefore = before, StockAfter = product.Stock, Reference = reference, Notes = notes, CreatedBy = "Offline ERP",
            SyncStatus = (int)SyncStatus.PendingSync, CreatedAt = now, UpdatedAt = now, IsDeleted = false
        });
    }

    private static async Task RestoreInvoiceStockAsync(AppDbContext db, Invoice entity, string notes, DateTime now)
    {
        var qty = entity.Items.Where(x => !x.IsDeleted).GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var products = await LoadProductsAsync(db, entity.CompanyId, qty.Keys);
        foreach (var product in products) ApplyStockChange(db, entity.CompanyId, product, qty[product.Id], StockMovementType.Return, entity.InvoiceNumber, notes, now);
    }

    private static Invoice ToEntity(InvoiceModel model, DateTime now) => new()
    {
        Id = model.LocalId, CompanyId = model.CompanyId, InvoiceNumber = model.InvoiceNumber, CustomerId = model.CustomerId, CustomerName = model.CustomerName,
        InvoiceDate = model.InvoiceDate, DueDate = model.DueDate, DiscountPct = model.DiscountPct, TaxPct = model.TaxPct,
        PaymentMethod = (int)model.PaymentMethod, PaidAmount = model.PaidAmount, Notes = model.Notes,
        Status = (int)model.Status, SyncStatus = (int)SyncStatus.PendingSync, CreatedAt = now, UpdatedAt = now, IsDeleted = false,
        Items = model.Items.Select(x => new InvoiceItem
        {
            Id = Guid.NewGuid(), InvoiceId = model.LocalId, ProductId = x.ProductId, ProductName = x.ProductName, SKU = x.SKU,
            UnitPrice = x.UnitPrice, Quantity = x.Quantity, DiscountPct = x.DiscountPct, TaxPct = x.TaxPct,
            CreatedAt = now, UpdatedAt = now, IsDeleted = false
        }).ToList()
    };

    private static InvoiceModel Map(Invoice entity)
    {
        var status = Enum.IsDefined(typeof(InvoiceStatus), entity.Status) ? (InvoiceStatus)entity.Status : InvoiceStatus.Draft;
        if (entity.DueDate.Date < DateTime.Today && status is InvoiceStatus.Sent or InvoiceStatus.PartialPaid)
            status = InvoiceStatus.Overdue;

        return new InvoiceModel
        {
            LocalId = entity.Id, CompanyId = entity.CompanyId, InvoiceNumber = entity.InvoiceNumber, CustomerId = entity.CustomerId,
            CustomerName = entity.CustomerName, InvoiceDate = entity.InvoiceDate, DueDate = entity.DueDate,
            DiscountPct = entity.DiscountPct, TaxPct = entity.TaxPct,
            PaymentMethod = Enum.IsDefined(typeof(PaymentMethod), entity.PaymentMethod) ? (PaymentMethod)entity.PaymentMethod : PaymentMethod.Cash,
            PaidAmount = entity.PaidAmount, Notes = entity.Notes, Status = status,
            SyncStatus = Enum.IsDefined(typeof(SyncStatus), entity.SyncStatus) ? (SyncStatus)entity.SyncStatus : SyncStatus.PendingSync,
            CreatedAt = entity.CreatedAt, UpdatedAt = entity.UpdatedAt ?? entity.CreatedAt, IsDeleted = entity.IsDeleted,
            Items = entity.Items.Where(x => !x.IsDeleted).Select(x => new InvoiceLineItem
            {
                ProductId = x.ProductId, ProductName = x.ProductName, SKU = x.SKU, UnitPrice = x.UnitPrice,
                Quantity = x.Quantity, DiscountPct = x.DiscountPct, TaxPct = x.TaxPct
            }).ToList()
        };
    }
}
