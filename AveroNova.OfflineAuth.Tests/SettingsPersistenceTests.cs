using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class SettingsPersistenceTests:IAsyncLifetime
{
 private string _path=null!;private IDbContextFactory<LocalAppDbContext> _factory=null!;private LocalSettingsService _service=null!;private readonly Guid _company=Guid.NewGuid(),_user=Guid.NewGuid();
 public async Task InitializeAsync(){_path=Path.Combine(Path.GetTempPath(),$"averonova-settings-{Guid.NewGuid():N}.db");var o=new DbContextOptionsBuilder<LocalAppDbContext>().UseSqlite($"Data Source={_path}").Options;_factory=new Factory(o);await using(var db=await _factory.CreateDbContextAsync()){await db.Database.EnsureCreatedAsync();}var s=new AppSessionContext();s.SetFromLocal(new LocalUserEntity{Id=_user,FullName="Settings User",Email="settings@test.local"},new LocalCompanyEntity{Id=_company,CompanyName="Settings Co"},["Owner"],["settings.manage"],Guid.NewGuid());_service=new LocalSettingsService(_factory,s);}
 public Task DisposeAsync(){try{if(File.Exists(_path))File.Delete(_path);}catch{}return Task.CompletedTask;}
 [Fact]public async Task Settings_CreateUpdatePersistAndQueueSync(){var settings=await _service.GetAsync();Assert.Equal(_company,settings.CompanyId);settings.Theme=ThemeMode.Dark;settings.Language="hi";settings.Currency="INR";settings.CurrencySymbol="₹";settings.TimeZone="Asia/Kolkata";settings.AutoSync=true;settings.OfflineMode=false;Assert.True((await _service.SaveAsync(settings)).Ok);var saved=await _service.GetAsync();Assert.Equal(ThemeMode.Dark,saved.Theme);Assert.Equal("INR",saved.Currency);saved.Notifications=false;Assert.True((await _service.SaveAsync(saved)).Ok);await using var db=await _factory.CreateDbContextAsync();Assert.Single(await db.AppSettings.ToListAsync());Assert.Equal(2,await db.SyncQueue.CountAsync());}
 [Fact]public async Task Settings_ValidateIsolationCurrencyColorAndOfflineConflict(){var settings=await _service.GetAsync();settings.CompanyId=Guid.NewGuid();Assert.False((await _service.SaveAsync(settings)).Ok);settings.CompanyId=_company;settings.AccentColor="blue";Assert.False((await _service.SaveAsync(settings)).Ok);settings.AccentColor="#2563EB";settings.Currency="INR";settings.CurrencySymbol="$";Assert.False((await _service.SaveAsync(settings)).Ok);settings.Currency="USD";settings.CurrencySymbol="$";settings.OfflineMode=true;settings.AutoSync=true;Assert.False((await _service.SaveAsync(settings)).Ok);}
 private sealed class Factory(DbContextOptions<LocalAppDbContext> o):IDbContextFactory<LocalAppDbContext>{public LocalAppDbContext CreateDbContext()=>new(o);public Task<LocalAppDbContext>CreateDbContextAsync(CancellationToken cancellationToken=default)=>Task.FromResult(CreateDbContext());}
}
