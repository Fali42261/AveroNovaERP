using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class PurchaseSupplierPersistenceTests : IAsyncLifetime
{
    private string _path = null!;
    private IDbContextFactory<LocalAppDbContext> _factory = null!;
    private AppSessionContext _session = null!;
    private LocalSupplierService _suppliers = null!;
    private LocalPurchaseService _purchases = null!;
    private readonly Guid _company = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _path=Path.Combine(Path.GetTempPath(),$"averonova-purchases-{Guid.NewGuid():N}.db");
        var options=new DbContextOptionsBuilder<LocalAppDbContext>().UseSqlite($"Data Source={_path}").Options;
        _factory=new Factory(options); await using(var db=await _factory.CreateDbContextAsync()){await db.Database.EnsureCreatedAsync();}
        _session=new AppSessionContext(); _session.SetFromLocal(new LocalUserEntity{Id=Guid.NewGuid(),FullName="Buyer",Email="buyer@test.local"},new LocalCompanyEntity{Id=_company,CompanyName="Test Company"},["Company.Owner"],["Purchases.Manage"],Guid.NewGuid());
        _suppliers=new LocalSupplierService(_factory,_session); _purchases=new LocalPurchaseService(_factory,_session);
    }

    public Task DisposeAsync(){try{if(File.Exists(_path))File.Delete(_path);}catch{}return Task.CompletedTask;}

    [Fact]
    public async Task SupplierAndPurchase_CrudPersistsAndQueuesSync()
    {
        var supplier=new SupplierModel{CompanyId=_company,Name="Parts Ltd",Email="parts@example.com"};
        Assert.True((await _suppliers.CreateAsync(supplier)).Ok);
        var purchase=new PurchaseModel{CompanyId=_company,PurchaseNumber=await _purchases.GetNextPurchaseNumberAsync(_company),SupplierId=supplier.LocalId,SupplierName=supplier.Name,PurchaseDate=DateTime.Today,DueDate=DateTime.Today.AddDays(7),Items=[new PurchaseLineItem{ProductId=Guid.NewGuid(),ProductName="Bearing",SKU="BR-1",Quantity=2,UnitPrice=50,TaxPct=10}],PaidAmount=20};
        Assert.True((await _purchases.CreateAsync(purchase)).Ok);
        var saved=await _purchases.GetByIdAsync(purchase.LocalId); Assert.NotNull(saved); Assert.Equal(110,saved.GrandTotal); Assert.Equal(90,saved.DueAmount);
        saved.Notes="Checked"; Assert.True((await _purchases.UpdateAsync(saved)).Ok);
        Assert.False((await _suppliers.DeleteAsync(supplier.LocalId)).Ok);
        Assert.True((await _purchases.DeleteAsync(purchase.LocalId)).Ok);
        Assert.True((await _suppliers.DeleteAsync(supplier.LocalId)).Ok);
        await using var db=await _factory.CreateDbContextAsync(); Assert.Equal(5,await db.SyncQueue.CountAsync());
    }

    [Fact]
    public async Task ValidationAndCompanyIsolation_AreEnforced()
    {
        Assert.False((await _suppliers.CreateAsync(new SupplierModel{CompanyId=Guid.NewGuid(),Name="Wrong company"})).Ok);
        Assert.False((await _suppliers.CreateAsync(new SupplierModel{CompanyId=_company,Name="Supplier",Email="bad email"})).Ok);
        Assert.Empty(await _purchases.GetAllAsync(Guid.NewGuid()));
        var invalid=new PurchaseModel{CompanyId=_company,PurchaseNumber="PO-X",PurchaseDate=DateTime.Today,DueDate=DateTime.Today.AddDays(-1)};
        Assert.False((await _purchases.CreateAsync(invalid)).Ok);
    }

    private sealed class Factory(DbContextOptions<LocalAppDbContext> options):IDbContextFactory<LocalAppDbContext>{public LocalAppDbContext CreateDbContext()=>new(options);public Task<LocalAppDbContext>CreateDbContextAsync(CancellationToken cancellationToken=default)=>Task.FromResult(CreateDbContext());}
}
