using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class ReturnPersistenceTests : IAsyncLifetime
{
    private readonly Guid _company = Guid.NewGuid();
    private readonly Guid _invoice = Guid.NewGuid();
    private readonly Guid _purchase = Guid.NewGuid();
    private readonly Guid _product = Guid.NewGuid();
    private string _path = null!;
    private IDbContextFactory<LocalAppDbContext> _factory = null!;
    private LocalReturnService _service = null!;

    public async Task InitializeAsync()
    {
        _path = Path.Combine(Path.GetTempPath(), $"averonova-returns-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LocalAppDbContext>().UseSqlite($"Data Source={_path}").Options;
        _factory = new Factory(options);
        await using (var db = await _factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
            db.Products.Add(new LocalProductEntity { Id=_product,CompanyId=_company,Name="Part",SKU="P-1",Stock=2 });
            db.Invoices.Add(new LocalInvoiceEntity { Id=_invoice,CompanyId=_company,InvoiceNumber="INV-1",CustomerId=Guid.NewGuid(),CustomerName="Customer",ItemsJson=JsonSerializer.Serialize(new[]{new InvoiceLineItem{ProductId=_product,ProductName="Item",Quantity=2,UnitPrice=50}}) });
            db.Purchases.Add(new LocalPurchaseEntity { Id=_purchase,CompanyId=_company,PurchaseNumber="PO-1",SupplierId=Guid.NewGuid(),SupplierName="Supplier",PurchaseDate=DateTime.Today,Status=(int)PurchaseStatus.Received,ItemsJson=JsonSerializer.Serialize(new[]{new PurchaseLineItem{ProductId=_product,ProductName="Part",Quantity=2,UnitPrice=40}}) });
            await db.SaveChangesAsync();
        }
        var session = new AppSessionContext();
        session.SetFromLocal(new LocalUserEntity{Id=Guid.NewGuid(),Email="r@test",FullName="Returns"},new LocalCompanyEntity{Id=_company,CompanyName="Returns Co"},["Owner"],["Returns.Manage"],Guid.NewGuid());
        _service = new LocalReturnService(_factory, session);
    }

    public Task DisposeAsync() { try { File.Delete(_path); } catch { } return Task.CompletedTask; }

    [Fact]
    public async Task SalesAndPendingPurchaseReturn_CrudAndSyncQueue_Work()
    {
        var sales = new SalesReturnModel { CompanyId=_company,InvoiceId=_invoice,ReturnDate=DateTime.Today,Reason="Defective",RefundAmount=60 };
        Assert.True((await _service.CreateSalesReturnAsync(sales)).Ok);
        sales.Status = ReturnStatus.Approved;
        Assert.True((await _service.UpdateSalesReturnAsync(sales)).Ok);
        var purchase = Return(1, 40, ReturnStatus.Pending);
        Assert.True((await _service.CreatePurchaseReturnAsync(purchase)).Ok);
        Assert.StartsWith("PR-", purchase.ReturnNumber);
        Assert.True((await _service.DeleteSalesReturnAsync(sales.LocalId)).Ok);
        Assert.True((await _service.DeletePurchaseReturnAsync(purchase.LocalId)).Ok);
        await using var db = await _factory.CreateDbContextAsync();
        Assert.Contains(await db.SyncQueue.ToListAsync(), x => x.EntityType == "PurchaseReturn");
    }

    [Fact]
    public async Task Returns_ValidateSourceAmountDateAndCompany()
    {
        Assert.False((await _service.CreateSalesReturnAsync(new SalesReturnModel{CompanyId=_company,InvoiceId=_invoice,ReturnDate=DateTime.Today,Reason="Bad",RefundAmount=101})).Ok);
        var future = Return(1, 40, ReturnStatus.Pending); future.ReturnDate = DateTime.Today.AddDays(1);
        Assert.False((await _service.CreatePurchaseReturnAsync(future)).Ok);
        Assert.Empty(await _service.GetSalesReturnsAsync(Guid.NewGuid()));
        Assert.Empty(await _service.GetPurchaseReturnsAsync(Guid.NewGuid()));
    }

    private PurchaseReturnModel Return(int quantity, decimal refund, ReturnStatus status) => new()
    {
        CompanyId=_company,PurchaseId=_purchase,ReturnDate=DateTime.Today,Reason="Wrong item",RefundAmount=refund,Status=status,
        Items=[new ReturnLineItem{ProductId=_product,ProductName="Part",Quantity=quantity,UnitPrice=40}]
    };

    private sealed class Factory(DbContextOptions<LocalAppDbContext> options) : IDbContextFactory<LocalAppDbContext>
    {
        public LocalAppDbContext CreateDbContext() => new(options);
    }
}
