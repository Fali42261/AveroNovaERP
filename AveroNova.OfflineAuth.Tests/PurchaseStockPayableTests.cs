using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class PurchaseStockPayableTests : IAsyncLifetime
{
    private readonly Guid _company = Guid.NewGuid();
    private readonly Guid _supplier = Guid.NewGuid();
    private readonly Guid _product = Guid.NewGuid();
    private string _path = null!;
    private IDbContextFactory<LocalAppDbContext> _factory = null!;
    private LocalPurchaseService _purchases = null!;
    private LocalPaymentService _payments = null!;

    public async Task InitializeAsync()
    {
        _path = Path.Combine(Path.GetTempPath(), $"averonova-purchase-stock-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LocalAppDbContext>().UseSqlite($"Data Source={_path}").Options;
        _factory = new Factory(options);
        await using (var db = await _factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
            db.Suppliers.Add(new LocalSupplierEntity { Id=_supplier,CompanyId=_company,Name="Supplier",IsActive=true });
            db.Products.Add(new LocalProductEntity { Id=_product,CompanyId=_company,Name="Bearing",SKU="BR-1",Stock=10 });
            await db.SaveChangesAsync();
        }
        var session = new AppSessionContext();
        session.SetFromLocal(new LocalUserEntity { Id=Guid.NewGuid(),FullName="Buyer",Email="buyer@test" },
            new LocalCompanyEntity { Id=_company,CompanyName="Company" }, ["Owner"], ["Purchases.Manage"], Guid.NewGuid());
        _purchases = new LocalPurchaseService(_factory, session);
        _payments = new LocalPaymentService(_factory, session);
    }

    public Task DisposeAsync() { try { File.Delete(_path); } catch { } return Task.CompletedTask; }

    [Fact]
    public async Task ReceivedPurchase_UpdatePaymentDeleteAndCancel_ReconcileAtomically()
    {
        var purchase = Purchase(5);
        Assert.True((await _purchases.CreateAsync(purchase)).Ok);
        await AssertStockAsync(15, 1);

        purchase.Items[0].Quantity = 8;
        Assert.True((await _purchases.UpdateAsync(purchase)).Ok);
        await AssertStockAsync(18, 2);

        var payment = Payment(purchase, 40m);
        Assert.True((await _payments.CreateAsync(payment)).Ok);
        Assert.Equal(40m, (await _purchases.GetByIdAsync(purchase.LocalId))!.PaidAmount);
        Assert.False((await _payments.CreateAsync(Payment(purchase, 50m))).Ok);
        Assert.False((await _purchases.DeleteAsync(purchase.LocalId)).Ok);

        Assert.True((await _payments.DeleteAsync(payment.LocalId)).Ok);
        purchase.PaidAmount = 0;
        purchase.Status = PurchaseStatus.Cancelled;
        Assert.True((await _purchases.UpdateAsync(purchase)).Ok);
        await AssertStockAsync(10, 3);
    }

    [Fact]
    public async Task ReversalThatWouldMakeStockNegative_IsRejectedWithoutPartialChanges()
    {
        var purchase = Purchase(5);
        Assert.True((await _purchases.CreateAsync(purchase)).Ok);
        await using (var db = await _factory.CreateDbContextAsync())
        {
            var product = await db.Products.SingleAsync(x => x.Id == _product);
            product.Stock = 2;
            await db.SaveChangesAsync();
        }
        purchase.Status = PurchaseStatus.Cancelled;
        var result = await _purchases.UpdateAsync(purchase);
        Assert.False(result.Ok);
        Assert.Contains("negative", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PurchaseStatus.Received, (await _purchases.GetByIdAsync(purchase.LocalId))!.Status);
        await AssertStockAsync(2, 1);
    }

    private PurchaseModel Purchase(int quantity) => new()
    {
        CompanyId=_company,PurchaseNumber=$"PO-{Guid.NewGuid():N}",SupplierId=_supplier,SupplierName="Supplier",
        PurchaseDate=DateTime.Today,DueDate=DateTime.Today.AddDays(10),Status=PurchaseStatus.Received,
        Items=[new PurchaseLineItem { ProductId=_product,ProductName="Bearing",SKU="BR-1",Quantity=quantity,UnitPrice=10 }]
    };

    private PaymentModel Payment(PurchaseModel purchase, decimal amount) => new()
    {
        CompanyId=_company,PartyName="Supplier",IsSupplier=true,InvoiceId=purchase.LocalId,
        Amount=amount,Method=PaymentMethod.BankTransfer,PaymentDate=DateTime.Today,Status=PaymentStatus.Completed
    };

    private async Task AssertStockAsync(int stock, int movements)
    {
        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(stock, (await db.Products.SingleAsync(x => x.Id == _product)).Stock);
        Assert.Equal(movements, await db.StockMovements.CountAsync(x => x.ProductId == _product));
    }

    private sealed class Factory(DbContextOptions<LocalAppDbContext> options) : IDbContextFactory<LocalAppDbContext>
    {
        public LocalAppDbContext CreateDbContext() => new(options);
    }
}
