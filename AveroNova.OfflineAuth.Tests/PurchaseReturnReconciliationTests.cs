using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class PurchaseReturnReconciliationTests : IAsyncLifetime
{
    private readonly Guid _company = Guid.NewGuid();
    private readonly Guid _purchase = Guid.NewGuid();
    private readonly Guid _product = Guid.NewGuid();
    private string _path = null!;
    private IDbContextFactory<LocalAppDbContext> _factory = null!;
    private LocalReturnService _returns = null!;

    public async Task InitializeAsync()
    {
        _path = Path.Combine(Path.GetTempPath(), $"averonova-purchase-return-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LocalAppDbContext>().UseSqlite($"Data Source={_path}").Options;
        _factory = new Factory(options);
        await using (var db = await _factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
            db.Products.Add(new LocalProductEntity { Id=_product,CompanyId=_company,Name="Bearing",SKU="BR-1",Stock=10 });
            db.Purchases.Add(new LocalPurchaseEntity
            {
                Id=_purchase,CompanyId=_company,PurchaseNumber="PO-RETURN",SupplierId=Guid.NewGuid(),SupplierName="Supplier",
                PurchaseDate=DateTime.Today,DueDate=DateTime.Today.AddDays(10),Status=(int)PurchaseStatus.Received,PaidAmount=50,
                ItemsJson=JsonSerializer.Serialize(new[]{new PurchaseLineItem{ProductId=_product,ProductName="Bearing",SKU="BR-1",Quantity=5,UnitPrice=20}})
            });
            await db.SaveChangesAsync();
        }
        var session = new AppSessionContext();
        session.SetFromLocal(new LocalUserEntity{Id=Guid.NewGuid(),Email="buyer@test",FullName="Buyer"},new LocalCompanyEntity{Id=_company,CompanyName="Company"},["Owner"],["Returns.Manage"],Guid.NewGuid());
        _returns = new LocalReturnService(_factory, session);
    }

    public Task DisposeAsync() { try { File.Delete(_path); } catch { } return Task.CompletedTask; }

    [Fact]
    public async Task CompleteEditAndDelete_ReconcileStockCreditAndRefundDue()
    {
        var model = Return(2, 40, ReturnStatus.Pending);
        Assert.True((await _returns.CreatePurchaseReturnAsync(model)).Ok);
        await AssertStateAsync(10, 0, 0);
        model.Status = ReturnStatus.Completed;
        Assert.True((await _returns.UpdatePurchaseReturnAsync(model)).Ok);
        await AssertStateAsync(8, 40, 1);
        Assert.Equal(10, (await PurchaseAsync()).DueAmount);

        model.Items[0].Quantity = 3;
        model.RefundAmount = 60;
        Assert.True((await _returns.UpdatePurchaseReturnAsync(model)).Ok);
        await AssertStateAsync(7, 60, 2);
        var purchase = await PurchaseAsync();
        Assert.Equal(0, purchase.DueAmount);
        Assert.Equal(10, purchase.SupplierRefundDueAmount);

        Assert.True((await _returns.DeletePurchaseReturnAsync(model.LocalId)).Ok);
        await AssertStateAsync(10, 0, 3);
    }

    [Fact]
    public async Task OverReturnAndNegativeStock_AreRejectedWithoutPartialChanges()
    {
        var first = Return(4, 80, ReturnStatus.Pending);
        Assert.True((await _returns.CreatePurchaseReturnAsync(first)).Ok);
        Assert.False((await _returns.CreatePurchaseReturnAsync(Return(2, 40, ReturnStatus.Pending))).Ok);
        Assert.True((await _returns.DeletePurchaseReturnAsync(first.LocalId)).Ok);
        await using (var db = await _factory.CreateDbContextAsync())
        {
            var product = await db.Products.SingleAsync(x => x.Id == _product);
            product.Stock = 1;
            await db.SaveChangesAsync();
        }
        var impossible = Return(2, 40, ReturnStatus.Completed);
        var result = await _returns.CreatePurchaseReturnAsync(impossible);
        Assert.False(result.Ok);
        Assert.Contains("negative", result.Error, StringComparison.OrdinalIgnoreCase);
        await AssertStateAsync(1, 0, 0);
        Assert.Null(await _returns.GetPurchaseReturnByIdAsync(impossible.LocalId));
    }

    private PurchaseReturnModel Return(int quantity, decimal refund, ReturnStatus status) => new()
    {
        CompanyId=_company,PurchaseId=_purchase,ReturnDate=DateTime.Today,Reason="Quality issue",RefundAmount=refund,Status=status,
        Items=[new ReturnLineItem{ProductId=_product,ProductName="Bearing",Quantity=quantity,UnitPrice=20}]
    };

    private async Task<PurchaseModel> PurchaseAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Purchases.SingleAsync(x => x.Id == _purchase);
        return new PurchaseModel { Items=JsonSerializer.Deserialize<List<PurchaseLineItem>>(row.ItemsJson)!,PaidAmount=row.PaidAmount,ReturnCreditAmount=row.ReturnCreditAmount };
    }

    private async Task AssertStateAsync(int stock, decimal credit, int movements)
    {
        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(stock, (await db.Products.SingleAsync(x => x.Id == _product)).Stock);
        Assert.Equal(credit, (await db.Purchases.SingleAsync(x => x.Id == _purchase)).ReturnCreditAmount);
        Assert.Equal(movements, await db.StockMovements.CountAsync(x => x.ProductId == _product));
    }

    private sealed class Factory(DbContextOptions<LocalAppDbContext> options) : IDbContextFactory<LocalAppDbContext>
    {
        public LocalAppDbContext CreateDbContext() => new(options);
    }
}
