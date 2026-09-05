using AveroNova.Application.DTOs.License;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services;
using AveroNova.App.UI.Services.Api;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.License;
using AveroNova.App.UI.Services.Security;
using AveroNova.Domain.Constants;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class OfflineLicenseAndBusinessTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private IDbContextFactory<LocalAppDbContext> _dbFactory = null!;
    private InstallationService _installation = null!;
    private AppSessionContext _session = null!;
    private LocalAuthSessionStore _sessions = null!;
    private FakeConnectivity _connectivity = null!;
    private FakeLicenseApi _licenseApi = null!;
    private FakeAnchorStore _anchor = null!;
    private FakeTokenStore _tokens = null!;
    private LicenseService _licenses = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"averonova-offbiz-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LocalAppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _dbFactory = new TestDbFactory(options);
        await using (var db = await _dbFactory.CreateDbContextAsync())
            await db.Database.EnsureCreatedAsync();

        _installation = new InstallationService(_dbFactory, NullLogger<InstallationService>.Instance);
        await _installation.EnsureInitializedAsync();
        _session = new AppSessionContext();
        _sessions = new LocalAuthSessionStore(_dbFactory);
        _connectivity = new FakeConnectivity(online: false);
        _licenseApi = new FakeLicenseApi();
        _anchor = new FakeAnchorStore();
        _tokens = new FakeTokenStore();
        _licenses = new LicenseService(
            _dbFactory,
            _licenseApi,
            _installation,
            _connectivity,
            _tokens,
            _anchor,
            NullLogger<LicenseService>.Instance);
    }

    public Task DisposeAsync()
    {
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task FirstLaunch_Offline_CreatesLocal15DayTrial_WithoutApi()
    {
        var status = await _licenses.EnsureActivatedAsync();
        Assert.Equal(LicenseBootstrapStatus.Ready, status);
        Assert.Equal(0, _licenseApi.InitializeCalls);

        var access = await _licenses.GetAccessStateAsync();
        Assert.True(access.AllowsAccess);
        Assert.False(access.NeedsFirstActivation);
        Assert.True(access.IsTrial);
        Assert.Equal(LicenseStatus.Trial, access.Status);
        Assert.Equal(LicenseConstants.TrialDays, (access.TrialEndDateUtc!.Value - access.TrialStartDateUtc!.Value).TotalDays, 0);
        Assert.NotNull(_anchor.Saved);
        Assert.Equal(_installation.DeviceId, _anchor.Saved!.DeviceId);
    }

    [Fact]
    public async Task LicenseApiUnavailable_DoesNotBlock_UsesLocalTrial()
    {
        _connectivity.SetOnline(true);
        _licenseApi.FailNetwork = true;

        var status = await _licenses.EnsureActivatedAsync();
        Assert.Equal(LicenseBootstrapStatus.Ready, status);
        var access = await _licenses.GetAccessStateAsync();
        Assert.True(access.AllowsAccess);
    }

    [Fact]
    public async Task ClockRollback_DoesNotExtendTrial()
    {
        Assert.Equal(LicenseBootstrapStatus.Ready, await _licenses.EnsureActivatedAsync());
        await using (var db = await _dbFactory.CreateDbContextAsync())
        {
            var row = await db.Licenses.SingleAsync();
            row.TrialStartDateUtc = DateTime.UtcNow.AddDays(-16);
            row.TrialEndDateUtc = DateTime.UtcNow.AddDays(-1);
            row.LastKnownTrustedTimeUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var access = await _licenses.GetAccessStateAsync();
        Assert.False(access.AllowsAccess);
        Assert.Equal(LicenseStatus.Expired, access.Status);
        Assert.Equal(LicenseBootstrapStatus.Blocked, await _licenses.EnsureActivatedAsync());
    }

    [Fact]
    public async Task OnlineSync_MakesServerAuthoritative_AndDoesNotResetExistingDeviceLicense()
    {
        await _licenses.EnsureActivatedAsync();
        var local = await _licenses.GetCachedStatusAsync();
        Assert.NotNull(local);

        _connectivity.SetOnline(true);
        _licenseApi.FailNetwork = false;
        _licenseApi.Response = new LicenseStatusResponse
        {
            LicenseId = local!.LicenseId,
            Plan = "Starter",
            Status = LicenseStatus.Trial,
            IsTrial = true,
            TrialStartDateUtc = local.TrialStartDateUtc,
            TrialEndDateUtc = local.TrialEndDateUtc,
            ExpiryDateUtc = local.ExpiryDateUtc,
            ServerTimeUtc = DateTime.UtcNow
        };

        await _licenses.SyncOnlineIfPossibleAsync();
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.Licenses.SingleAsync();
        Assert.True(row.IsServerAuthoritative);
        Assert.Equal(local.TrialStartDateUtc, row.TrialStartDateUtc);
    }

    [Fact]
    public async Task BusinessData_CreatedOffline_IsPending_AndCompanyScoped()
    {
        var userId = Guid.NewGuid();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        await SeedUserWithCompaniesAsync(userId, companyA, companyB);
        _session.SetFromLocal(
            new LocalUserEntity { Id = userId, FullName = "Owner", Email = "o@t.local", IsActive = true },
            new LocalCompanyEntity { Id = companyA, CompanyName = "A", IsActive = true },
            ["Company Owner"],
            OfflineRegistrationStore.OwnerPermissions,
            Guid.NewGuid());

        var customers = new LocalCustomerService(_dbFactory, _session);
        var products = new LocalProductService(_dbFactory, _session);
        var billing = new LocalBillingService(_dbFactory, _session);
        var payments = new LocalPaymentService(_dbFactory, _session);

        Assert.True((await customers.CreateAsync(new CustomerModel { CompanyId = companyA, Name = "Acme" })).Ok);
        Assert.False((await customers.CreateAsync(new CustomerModel { CompanyId = companyB, Name = "Other Co" })).Ok);
        Assert.True((await products.CreateAsync(new ProductModel { CompanyId = companyA, Name = "Widget", SKU = "W1", SellingPrice = 10 })).Ok);
        Assert.True((await billing.CreateAsync(new InvoiceModel
        {
            CompanyId = companyA,
            CustomerName = "Acme",
            Items = [new InvoiceLineItem { ProductName = "Widget", Quantity = 1, UnitPrice = 10 }]
        })).Ok);
        Assert.True((await payments.CreateAsync(new PaymentModel { CompanyId = companyA, PartyName = "Acme", Amount = 10 })).Ok);

        Assert.Single(await customers.GetAllAsync(companyA));
        Assert.Empty(await customers.GetAllAsync(companyB));
        Assert.Single(await products.GetAllAsync(companyA));
        Assert.Single(await billing.GetAllAsync(companyA));
        Assert.Single(await payments.GetAllAsync(companyA));

        await using var db = await _dbFactory.CreateDbContextAsync();
        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Equal(1, await db.Products.CountAsync());
        Assert.Equal(1, await db.Invoices.CountAsync());
        Assert.Equal(1, await db.Payments.CountAsync());
        var queue = await db.SyncQueue.ToListAsync();
        Assert.Contains(queue, q => q.EntityType == "Customer" && q.Status == (int)RecordSyncStatus.Pending);
        Assert.Contains(queue, q => q.EntityType == "Product" && q.Status == (int)RecordSyncStatus.Pending);
        Assert.Contains(queue, q => q.EntityType == "Invoice" && q.Status == (int)RecordSyncStatus.Pending);
        Assert.Contains(queue, q => q.EntityType == "Payment" && q.Status == (int)RecordSyncStatus.Pending);
        Assert.DoesNotContain(queue, q => q.Status == (int)RecordSyncStatus.Synced);
    }

    [Fact]
    public async Task CompanySwitch_Offline_ReloadsPermissions_AndScopesData()
    {
        var userId = Guid.NewGuid();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        await SeedUserWithCompaniesAsync(userId, companyA, companyB);
        var snapshot = await _sessions.EstablishOfflineSessionAsync(_installation.InstallationId, _installation.DeviceId, userId, companyA);
        Assert.NotNull(snapshot);
        _session.SetFromLocal(snapshot!.User, snapshot.Company, snapshot.Roles, snapshot.Permissions, snapshot.Session.ServerSessionId);

        var customers = new LocalCustomerService(_dbFactory, _session);
        Assert.True((await customers.CreateAsync(new CustomerModel { CompanyId = companyA, Name = "A Cust" })).Ok);

        var switched = await _sessions.SwitchCompanyAsync(_installation.InstallationId, userId, companyB);
        Assert.NotNull(switched);
        _session.SetFromLocal(switched!.User, switched.Company, switched.Roles, switched.Permissions, switched.Session.ServerSessionId);
        Assert.Equal(companyB, _session.CurrentCompanyId);
        Assert.True(_session.HasPermission("Dashboard.View"));
        Assert.Empty(await customers.GetAllAsync(companyA));
        Assert.Empty(await customers.GetAllAsync(companyB));
        Assert.True((await customers.CreateAsync(new CustomerModel { CompanyId = companyB, Name = "B Cust" })).Ok);
        Assert.Single(await customers.GetAllAsync(companyB));
    }

    [Fact]
    public void MenuCatalog_IsPermissionDriven_NotRoleNames()
    {
        var sales = new[] { "Dashboard.View", "Sales.View", "Customers.View" };
        Assert.True(MenuCatalog.IsAllowed("Dashboard", sales));
        Assert.True(MenuCatalog.IsAllowed("Billing", sales));
        Assert.True(MenuCatalog.IsAllowed("Customers", sales));
        Assert.False(MenuCatalog.IsAllowed("Users", sales));
        Assert.True(MenuCatalog.IsAllowed("License", sales));
        Assert.Contains(MenuCatalog.Items, i => i.Key == "Users" && i.RequiredPermission == "Users.Manage");
    }

    private async Task SeedUserWithCompaniesAsync(Guid userId, Guid companyA, Guid companyB)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Users.Add(new LocalUserEntity { Id = userId, FullName = "Owner", Email = "owner@test.local", IsActive = true });
        db.Companies.Add(new LocalCompanyEntity { Id = companyA, CompanyName = "Company A", IsActive = true });
        db.Companies.Add(new LocalCompanyEntity { Id = companyB, CompanyName = "Company B", IsActive = true });
        db.UserCompanies.Add(new LocalUserCompanyEntity { Id = Guid.NewGuid(), UserId = userId, CompanyId = companyA, IsDefault = true, IsOwner = true, IsActive = true });
        db.UserCompanies.Add(new LocalUserCompanyEntity { Id = Guid.NewGuid(), UserId = userId, CompanyId = companyB, IsDefault = false, IsOwner = true, IsActive = true });
        foreach (var companyId in new[] { companyA, companyB })
        {
            db.Roles.Add(new LocalRoleEntity { Id = Guid.NewGuid(), UserId = userId, CompanyId = companyId, RoleName = "Company Owner" });
            foreach (var permission in OfflineRegistrationStore.OwnerPermissions)
            {
                db.Permissions.Add(new LocalPermissionEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CompanyId = companyId,
                    PermissionName = permission
                });
            }
        }
        await db.SaveChangesAsync();
    }

    private sealed class TestDbFactory(DbContextOptions<LocalAppDbContext> options) : IDbContextFactory<LocalAppDbContext>
    {
        public LocalAppDbContext CreateDbContext() => new(options);
        public Task<LocalAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class FakeConnectivity(bool online) : IConnectivityService
    {
        private ConnectivityStatus _status = online ? ConnectivityStatus.Online : ConnectivityStatus.Offline;
        public ConnectivityStatus Status => _status;
        public bool IsOnline => _status is ConnectivityStatus.Online or ConnectivityStatus.Synced or ConnectivityStatus.Syncing;
        public int PendingCount => 0;
        public event EventHandler<ConnectivityStatus>? StatusChanged;
        public void UpdateStatus(ConnectivityStatus status)
        {
            _status = status;
            StatusChanged?.Invoke(this, status);
        }
        public void SetOnline(bool online) => UpdateStatus(online ? ConnectivityStatus.Online : ConnectivityStatus.Offline);
        public void IncrementPending() { }
        public void DecrementPending(int count = 1) { }
    }

    private sealed class FakeTokenStore : ISecureTokenStore
    {
        public Task SetAccessTokenAsync(string token, DateTime expiresUtc) => Task.CompletedTask;
        public Task SetRefreshTokenAsync(string token) => Task.CompletedTask;
        public Task SetSessionIdAsync(Guid sessionId) => Task.CompletedTask;
        public Task<string?> GetAccessTokenAsync() => Task.FromResult<string?>(null);
        public Task<string?> GetRefreshTokenAsync() => Task.FromResult<string?>(null);
        public Task<DateTime?> GetAccessTokenExpiryAsync() => Task.FromResult<DateTime?>(null);
        public Task<Guid?> GetSessionIdAsync() => Task.FromResult<Guid?>(null);
        public Task ClearAsync() => Task.CompletedTask;
    }

    private sealed class FakeAnchorStore : ILicenseAnchorStore
    {
        public LicenseAnchor? Saved { get; private set; }
        public Task SaveAsync(LicenseAnchor anchor) { Saved = anchor; return Task.CompletedTask; }
        public Task<LicenseAnchor?> LoadAsync() => Task.FromResult(Saved);
    }

    private sealed class FakeLicenseApi : ILicenseApiClient
    {
        public int InitializeCalls;
        public bool FailNetwork;
        public LicenseStatusResponse? Response;

        public Task<ApiCallResult<LicenseStatusResponse>> InitializeAsync(LicenseInitializeRequest request, string? bearerToken = null, CancellationToken cancellationToken = default)
        {
            InitializeCalls++;
            if (FailNetwork)
                return Task.FromResult(ApiCallResult<LicenseStatusResponse>.Fail(0, "Unable to connect to the server.", network: true));
            return Task.FromResult(ApiCallResult<LicenseStatusResponse>.Ok(Response ?? new LicenseStatusResponse
            {
                LicenseId = request.ClientLicenseId ?? Guid.NewGuid(),
                Plan = "Starter",
                Status = LicenseStatus.Trial,
                IsTrial = true,
                TrialStartDateUtc = DateTime.UtcNow,
                TrialEndDateUtc = DateTime.UtcNow.AddDays(15),
                ServerTimeUtc = DateTime.UtcNow
            }, 200));
        }

        public Task<ApiCallResult<LicenseStatusResponse>> GetStatusAsync(string deviceId, string bearerToken, CancellationToken cancellationToken = default)
            => InitializeAsync(new LicenseInitializeRequest { DeviceId = deviceId }, bearerToken, cancellationToken);

        public Task<ApiCallResult<LicenseStatusResponse>> ValidateAsync(LicenseValidateRequest request, string bearerToken, CancellationToken cancellationToken = default)
            => InitializeAsync(new LicenseInitializeRequest { DeviceId = request.DeviceId }, bearerToken, cancellationToken);

        public Task<ApiCallResult<LicenseStatusResponse>> SyncAsync(LicenseSyncRequest request, string bearerToken, CancellationToken cancellationToken = default)
            => InitializeAsync(new LicenseInitializeRequest { DeviceId = request.DeviceId }, bearerToken, cancellationToken);
    }
}
