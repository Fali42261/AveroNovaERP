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
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Payments.AsNoTracking().Where(x => x.CompanyId == companyId && !x.IsDeleted).OrderByDescending(x => x.PaymentDate).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<PaymentModel?> GetByIdAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        return row == null ? null : Map(row);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(PaymentModel payment)
    {
        if (payment.CompanyId == Guid.Empty) return (false, "Company is required.");
        if (payment.Amount <= 0) return (false, "Payment amount must be greater than zero.");
        await using var db = await _factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        payment.LocalId = payment.LocalId == Guid.Empty ? Guid.NewGuid() : payment.LocalId;
        payment.SyncStatus = SyncStatus.PendingSync;
        db.Payments.Add(ToEntity(payment, now));
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(PaymentModel payment)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Payments.FirstOrDefaultAsync(x => x.Id == payment.LocalId && !x.IsDeleted);
        if (row == null) return (false, "Payment not found.");
        if (payment.Amount <= 0) return (false, "Payment amount must be greater than zero.");
        row.Amount = payment.Amount; row.PaymentDate = payment.PaymentDate; row.Method = (int)payment.Method;
        row.PartyId = payment.PartyId; row.PartyName = payment.PartyName; row.IsSupplier = payment.IsSupplier;
        row.InvoiceId = payment.InvoiceId; row.InvoiceNumber = payment.InvoiceNumber;
        row.Reference = payment.Reference; row.Notes = payment.Notes; row.Status = (int)payment.Status;
        row.UpdatedAt = DateTime.UtcNow; row.SyncStatus = (int)SyncStatus.PendingSync;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Payments.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (row == null) return (false, "Payment not found.");
        row.IsDeleted = true; row.UpdatedAt = DateTime.UtcNow; row.SyncStatus = (int)SyncStatus.PendingSync;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<string> GetNextPaymentNumberAsync(Guid companyId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var count = await db.Payments.CountAsync(x => x.CompanyId == companyId && !x.IsDeleted);
        return $"PAY-{DateTime.Today:yyyy}-{count + 1:D4}";
    }

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
        LocalId = x.Id, CompanyId = x.CompanyId, PaymentNumber = x.PaymentNumber, PartyId = x.PartyId,
        PartyName = x.PartyName, IsSupplier = x.IsSupplier, InvoiceId = x.InvoiceId, InvoiceNumber = x.InvoiceNumber,
        Amount = x.Amount, PaymentDate = x.PaymentDate,
        Method = Enum.IsDefined(typeof(PaymentMethod), x.Method) ? (PaymentMethod)x.Method : PaymentMethod.Cash,
        Reference = x.Reference, Notes = x.Notes,
        Status = Enum.IsDefined(typeof(PaymentStatus), x.Status) ? (PaymentStatus)x.Status : PaymentStatus.Completed,
        SyncStatus = Enum.IsDefined(typeof(SyncStatus), x.SyncStatus) ? (SyncStatus)x.SyncStatus : SyncStatus.PendingSync,
        CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt ?? x.CreatedAt, IsDeleted = x.IsDeleted
    };
}
