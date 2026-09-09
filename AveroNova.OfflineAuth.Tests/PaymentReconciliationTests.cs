using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class PaymentReconciliationTests : IAsyncLifetime
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _otherCompanyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _invoiceId = Guid.NewGuid();
    private string _path = null!;
    private IDbContextFactory<LocalAppDbContext> _factory = null!;
    private LocalPaymentService _payments = null!;
    private LocalBillingService _billing = null!;

    public async Task InitializeAsync()
    {
        _path = Path.Combine(Path.GetTempPath(), $"averonova-payment-reconcile-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LocalAppDbContext>()
            .UseSqlite($"Data Source={_path}").Options;
        _factory = new Factory(options);
        await using (var db = await _factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
            db.Invoices.AddRange(
                Invoice(_invoiceId, _companyId, "INV-1", _customerId, 100m),
                Invoice(Guid.NewGuid(), _otherCompanyId, "INV-OTHER", Guid.NewGuid(), 500m));
            await db.SaveChangesAsync();
        }

        var session = new AppSessionContext();
        session.SetFromLocal(
            new LocalUserEntity { Id = Guid.NewGuid(), FullName = "Owner", Email = "owner@test.local" },
            new LocalCompanyEntity { Id = _companyId, CompanyName = "Current" },
            ["Company Owner"], ["Sales.Create"], Guid.NewGuid());
        _payments = new LocalPaymentService(_factory, session);
        _billing = new LocalBillingService(_factory, session);
    }

    public Task DisposeAsync()
    {
        try { File.Delete(_path); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CompletedPayments_CreateUpdateDelete_ReconcileInvoiceAtomically()
    {
        var payment = Payment(40m);
        var created = await _payments.CreateAsync(payment);
        Assert.True(created.Ok, created.Error);
        await AssertInvoiceAsync(40m, InvoiceStatus.PartialPaid);

        payment.Amount = 100m;
        var updated = await _payments.UpdateAsync(payment);
        Assert.True(updated.Ok, updated.Error);
        await AssertInvoiceAsync(100m, InvoiceStatus.Paid);

        var deleted = await _payments.DeleteAsync(payment.LocalId);
        Assert.True(deleted.Ok, deleted.Error);
        await AssertInvoiceAsync(0m, InvoiceStatus.Sent);

        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(3, await db.SyncQueue.CountAsync(q => q.EntityType == "Payment"));
        Assert.Equal(3, await db.SyncQueue.CountAsync(q => q.EntityType == "Invoice"));
        Assert.All(await db.SyncQueue.Where(q => q.EntityType == "Payment").ToListAsync(),
            q => Assert.Equal((int)RecordSyncStatus.Pending, q.Status));
    }

    [Fact]
    public async Task Overpayment_IsRejected_WithoutChangingInvoiceOrQueue()
    {
        Assert.True((await _payments.CreateAsync(Payment(60m))).Ok);
        var rejected = await _payments.CreateAsync(Payment(41m));

        Assert.False(rejected.Ok);
        Assert.Contains("exceeds", rejected.Error, StringComparison.OrdinalIgnoreCase);
        await AssertInvoiceAsync(60m, InvoiceStatus.PartialPaid);

        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(1, await db.Payments.CountAsync());
        Assert.Equal(1, await db.SyncQueue.CountAsync(q => q.EntityType == "Payment"));
    }

    [Fact]
    public async Task PendingPayment_DoesNotAffectPaidAmount_UntilCompleted()
    {
        var payment = Payment(75m, PaymentStatus.Pending);
        Assert.True((await _payments.CreateAsync(payment)).Ok);
        await AssertInvoiceAsync(0m, InvoiceStatus.Sent);

        payment.Status = PaymentStatus.Completed;
        Assert.True((await _payments.UpdateAsync(payment)).Ok);
        await AssertInvoiceAsync(75m, InvoiceStatus.PartialPaid);
    }

    [Fact]
    public async Task ForeignOrSupplierInvoiceLink_IsRejected()
    {
        Guid foreignInvoiceId;
        await using (var db = await _factory.CreateDbContextAsync())
            foreignInvoiceId = await db.Invoices.Where(i => i.CompanyId == _otherCompanyId).Select(i => i.Id).SingleAsync();

        var foreign = Payment(10m);
        foreign.InvoiceId = foreignInvoiceId;
        Assert.False((await _payments.CreateAsync(foreign)).Ok);

        var supplier = Payment(10m);
        supplier.IsSupplier = true;
        var result = await _payments.CreateAsync(supplier);
        Assert.False(result.Ok);
        Assert.Contains("Purchase", result.Error);
    }

    [Fact]
    public async Task InvoiceWithPayments_CannotShrinkBelowPaid_CancelOrDelete()
    {
        Assert.True((await _payments.CreateAsync(Payment(60m))).Ok);
        var invoice = await _billing.GetByIdAsync(_invoiceId);
        Assert.NotNull(invoice);
        invoice!.Items[0].UnitPrice = 50m;

        Assert.False((await _billing.UpdateAsync(invoice)).Ok);
        Assert.False((await _billing.CancelAsync(_invoiceId)).Ok);
        Assert.False((await _billing.DeleteAsync(_invoiceId)).Ok);
        await AssertInvoiceAsync(60m, InvoiceStatus.PartialPaid);
    }

    private PaymentModel Payment(decimal amount, PaymentStatus status = PaymentStatus.Completed)
        => new()
        {
            CompanyId = _companyId,
            PartyName = "Customer",
            InvoiceId = _invoiceId,
            Amount = amount,
            PaymentDate = DateTime.Today,
            Method = PaymentMethod.Cash,
            Status = status
        };

    private async Task AssertInvoiceAsync(decimal paid, InvoiceStatus status)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var invoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == _invoiceId);
        Assert.Equal(paid, invoice.PaidAmount);
        Assert.Equal((int)status, invoice.Status);
    }

    private static LocalInvoiceEntity Invoice(Guid id, Guid companyId, string number, Guid customerId, decimal total)
        => new()
        {
            Id = id,
            CompanyId = companyId,
            InvoiceNumber = number,
            CustomerId = customerId,
            CustomerName = "Customer",
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            Status = (int)InvoiceStatus.Sent,
            ItemsJson = JsonSerializer.Serialize(new[]
            {
                new InvoiceLineItem { ProductId = Guid.NewGuid(), ProductName = "Item", Quantity = 1, UnitPrice = total }
            })
        };

    private sealed class Factory(DbContextOptions<LocalAppDbContext> options) : IDbContextFactory<LocalAppDbContext>
    {
        public LocalAppDbContext CreateDbContext() => new(options);
    }
}
