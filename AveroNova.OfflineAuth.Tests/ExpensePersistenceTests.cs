using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class ExpensePersistenceTests:IAsyncLifetime
{
 private string _path=null!;private IDbContextFactory<LocalAppDbContext> _factory=null!;private LocalExpenseService _service=null!;private readonly Guid _company=Guid.NewGuid();
 public async Task InitializeAsync(){_path=Path.Combine(Path.GetTempPath(),$"averonova-expenses-{Guid.NewGuid():N}.db");var options=new DbContextOptionsBuilder<LocalAppDbContext>().UseSqlite($"Data Source={_path}").Options;_factory=new Factory(options);await using(var db=await _factory.CreateDbContextAsync()){await db.Database.EnsureCreatedAsync();}var session=new AppSessionContext();session.SetFromLocal(new LocalUserEntity{Id=Guid.NewGuid(),FullName="Approver",Email="approver@test.local"},new LocalCompanyEntity{Id=_company,CompanyName="Expense Company"},["Company.Owner"],["Expenses.Manage"],Guid.NewGuid());_service=new LocalExpenseService(_factory,session);}
 public Task DisposeAsync(){try{if(File.Exists(_path))File.Delete(_path);}catch{}return Task.CompletedTask;}

 [Fact]public async Task ExpenseCrud_PersistsAndQueuesEveryOperation(){var expense=new ExpenseModel{CompanyId=_company,Category="Travel",Amount=250,ExpenseDate=DateTime.Today,Method=PaymentMethod.BankTransfer,Status=ExpenseStatus.Pending,Description="Client visit"};Assert.True((await _service.CreateAsync(expense)).Ok);var saved=await _service.GetByIdAsync(expense.LocalId);Assert.NotNull(saved);Assert.Equal(250,saved.Amount);saved.Status=ExpenseStatus.Paid;saved.ApprovedBy="Manager";Assert.True((await _service.UpdateAsync(saved)).Ok);Assert.True((await _service.DeleteAsync(saved.LocalId)).Ok);await using var db=await _factory.CreateDbContextAsync();Assert.Empty(await db.Expenses.ToListAsync());Assert.Equal(3,await db.SyncQueue.CountAsync());}
 [Fact]public async Task ExpenseValidationAndCompanyIsolation_AreEnforced(){Assert.False((await _service.CreateAsync(new ExpenseModel{CompanyId=Guid.NewGuid(),Category="Travel",Amount=1})).Ok);Assert.False((await _service.CreateAsync(new ExpenseModel{CompanyId=_company,Category="Travel",Amount=0})).Ok);Assert.False((await _service.CreateAsync(new ExpenseModel{CompanyId=_company,Category="Travel",Amount=1,ExpenseDate=DateTime.Today.AddDays(1)})).Ok);Assert.False((await _service.CreateAsync(new ExpenseModel{CompanyId=_company,Category="Travel",Amount=1,Status=ExpenseStatus.Approved})).Ok);Assert.Empty(await _service.GetAllAsync(Guid.NewGuid()));}
 private sealed class Factory(DbContextOptions<LocalAppDbContext> options):IDbContextFactory<LocalAppDbContext>{public LocalAppDbContext CreateDbContext()=>new(options);public Task<LocalAppDbContext>CreateDbContextAsync(CancellationToken cancellationToken=default)=>Task.FromResult(CreateDbContext());}
}
