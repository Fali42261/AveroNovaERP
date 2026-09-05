using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class ReturnPersistenceTests:IAsyncLifetime
{
 private string _path=null!;private IDbContextFactory<LocalAppDbContext> _factory=null!;private LocalReturnService _service=null!;private readonly Guid _company=Guid.NewGuid(),_invoice=Guid.NewGuid(),_purchase=Guid.NewGuid();
 public async Task InitializeAsync(){_path=Path.Combine(Path.GetTempPath(),$"averonova-returns-{Guid.NewGuid():N}.db");var o=new DbContextOptionsBuilder<LocalAppDbContext>().UseSqlite($"Data Source={_path}").Options;_factory=new Factory(o);await using(var db=await _factory.CreateDbContextAsync()){await db.Database.EnsureCreatedAsync();db.Invoices.Add(new LocalInvoiceEntity{Id=_invoice,CompanyId=_company,InvoiceNumber="INV-1",CustomerId=Guid.NewGuid(),CustomerName="Customer",ItemsJson=JsonSerializer.Serialize(new[]{new InvoiceLineItem{ProductId=Guid.NewGuid(),ProductName="Item",Quantity=2,UnitPrice=50}})});db.Purchases.Add(new LocalPurchaseEntity{Id=_purchase,CompanyId=_company,PurchaseNumber="PO-1",SupplierId=Guid.NewGuid(),SupplierName="Supplier",ItemsJson=JsonSerializer.Serialize(new[]{new PurchaseLineItem{ProductId=Guid.NewGuid(),ProductName="Part",Quantity=2,UnitPrice=40}})});await db.SaveChangesAsync();}var s=new AppSessionContext();s.SetFromLocal(new LocalUserEntity{Id=Guid.NewGuid(),Email="r@test",FullName="Returns"},new LocalCompanyEntity{Id=_company,CompanyName="Returns Co"},["Owner"],["Returns.Manage"],Guid.NewGuid());_service=new LocalReturnService(_factory,s);}
 public Task DisposeAsync(){try{if(File.Exists(_path))File.Delete(_path);}catch{}return Task.CompletedTask;}
 [Fact]public async Task SalesAndPurchaseReturn_CrudAndSyncQueue_Work(){var sr=new SalesReturnModel{CompanyId=_company,InvoiceId=_invoice,ReturnDate=DateTime.Today,Reason="Defective",RefundAmount=60};Assert.True((await _service.CreateSalesReturnAsync(sr)).Ok);Assert.StartsWith("SR-",sr.ReturnNumber);sr.Status=ReturnStatus.Approved;Assert.True((await _service.UpdateSalesReturnAsync(sr)).Ok);var pr=new PurchaseReturnModel{CompanyId=_company,PurchaseId=_purchase,ReturnDate=DateTime.Today,Reason="Wrong item",RefundAmount=50};Assert.True((await _service.CreatePurchaseReturnAsync(pr)).Ok);Assert.StartsWith("PR-",pr.ReturnNumber);Assert.True((await _service.DeleteSalesReturnAsync(sr.LocalId)).Ok);Assert.True((await _service.DeletePurchaseReturnAsync(pr.LocalId)).Ok);await using var db=await _factory.CreateDbContextAsync();Assert.Equal(5,await db.SyncQueue.CountAsync());}
 [Fact]public async Task Returns_ValidateSourceAmountDateAndCompany(){Assert.False((await _service.CreateSalesReturnAsync(new SalesReturnModel{CompanyId=_company,InvoiceId=_invoice,ReturnDate=DateTime.Today,Reason="Bad",RefundAmount=101})).Ok);Assert.False((await _service.CreatePurchaseReturnAsync(new PurchaseReturnModel{CompanyId=_company,PurchaseId=_purchase,ReturnDate=DateTime.Today.AddDays(1),Reason="Bad",RefundAmount=1})).Ok);Assert.Empty(await _service.GetSalesReturnsAsync(Guid.NewGuid()));Assert.Empty(await _service.GetPurchaseReturnsAsync(Guid.NewGuid()));}
 private sealed class Factory(DbContextOptions<LocalAppDbContext> o):IDbContextFactory<LocalAppDbContext>{public LocalAppDbContext CreateDbContext()=>new(o);public Task<LocalAppDbContext>CreateDbContextAsync(CancellationToken cancellationToken=default)=>Task.FromResult(CreateDbContext());}
}
