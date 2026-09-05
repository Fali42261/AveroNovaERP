using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class SubscriptionPersistenceTests:IAsyncLifetime
{
 private string _path=null!;private IDbContextFactory<LocalAppDbContext> _factory=null!;private LocalSubscriptionService _service=null!;private readonly Guid _company=Guid.NewGuid();
 public async Task InitializeAsync(){_path=Path.Combine(Path.GetTempPath(),$"averonova-subscription-{Guid.NewGuid():N}.db");var o=new DbContextOptionsBuilder<LocalAppDbContext>().UseSqlite($"Data Source={_path}").Options;_factory=new Factory(o);await using(var db=await _factory.CreateDbContextAsync()){await db.Database.EnsureCreatedAsync();db.Subscriptions.Add(new LocalSubscriptionEntity{Id=Guid.NewGuid(),CompanyId=_company,PlanId="starter",PlanName="Starter",StartDateUtc=DateTime.UtcNow.AddDays(-2),EndDateUtc=DateTime.UtcNow.AddDays(13),IsTrial=true,IsActive=true,Status=(int)SubscriptionStatus.Active,MaxUsers=2,MaxCompanies=1,UpdatedAtUtc=DateTime.UtcNow});await db.SaveChangesAsync();}var session=new AppSessionContext();session.SetFromLocal(new LocalUserEntity{Id=Guid.NewGuid(),FullName="Subscriber",Email="sub@test.local"},new LocalCompanyEntity{Id=_company,CompanyName="Subscriber Co"},["Owner"],["subscription.view"],Guid.NewGuid());_service=new LocalSubscriptionService(_factory,session);}
 public Task DisposeAsync(){try{if(File.Exists(_path))File.Delete(_path);}catch{}return Task.CompletedTask;}
 [Fact]public async Task UpgradeAndCancel_PersistPaymentAndQueueSync(){var current=await _service.GetCurrentAsync(_company);Assert.NotNull(current);Assert.Equal("starter",current.PlanId);var result=await _service.UpgradeAsync(_company,"pro",BillingCycle.Yearly);Assert.True(result.Ok,result.Error);var upgraded=await _service.GetCurrentAsync();Assert.NotNull(upgraded);Assert.Equal("pro",upgraded.PlanId);Assert.Equal(9999,upgraded.Price);var history=await _service.GetPaymentHistoryAsync(_company);Assert.Single(history);Assert.Equal("Pending",history[0].Status);Assert.True((await _service.CancelAsync(_company)).Ok);Assert.Equal(SubscriptionStatus.Cancelled,(await _service.GetCurrentAsync())!.Status);await using var db=await _factory.CreateDbContextAsync();Assert.Equal(3,await db.SyncQueue.CountAsync());}
 [Fact]public async Task PlansAndCompanyValidation_AreEnforced(){var plans=await _service.GetPlansAsync();Assert.Equal(3,plans.Count);Assert.True(plans.Single(x=>x.Id=="starter").IsCurrentPlan);Assert.Null(await _service.GetCurrentAsync(Guid.NewGuid()));Assert.Empty(await _service.GetPaymentHistoryAsync(Guid.NewGuid()));Assert.False((await _service.UpgradeAsync(Guid.NewGuid(),"pro",BillingCycle.Monthly)).Ok);Assert.False((await _service.UpgradeAsync(_company,"missing",BillingCycle.Monthly)).Ok);}
 private sealed class Factory(DbContextOptions<LocalAppDbContext> o):IDbContextFactory<LocalAppDbContext>{public LocalAppDbContext CreateDbContext()=>new(o);public Task<LocalAppDbContext>CreateDbContextAsync(CancellationToken cancellationToken=default)=>Task.FromResult(CreateDbContext());}
}
