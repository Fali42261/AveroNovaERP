using AveroNova.Application.DTOs.Auth;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using AveroNova.App.UI.Services.Api;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Security;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

/// <summary>
/// Executes the mandatory offline → local SQLite → Pending → Sync API → server SQLite → Synced → online login chain.
/// </summary>
public sealed class AuthFinalOfflineSyncGateTests : IAsyncLifetime
{
    private GateWebApplicationFactory _factory = null!;
    private string _localDbPath = null!;
    private IDbContextFactory<LocalAppDbContext> _localFactory = null!;
    private FakePendingSecrets _pendingSecrets = null!;
    private IAuthApiClient _authApi = null!;
    private OfflineRegistrationStore _offlineReg = null!;
    private string _email = null!;
    private string _password = null!;
    private string _companyName = null!;
    private Guid _userId;
    private Guid _companyId;
    private Guid _userCompanyId;
    private Guid _subscriptionId;
    private Guid _installationId;
    private string _deviceId = null!;

    public async Task InitializeAsync()
    {
        _factory = new GateWebApplicationFactory();
        _ = _factory.Server; // force host start + migrations

        _localDbPath = Path.Combine(Path.GetTempPath(), $"AveroNovaLocal-gate-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LocalAppDbContext>()
            .UseSqlite($"Data Source={_localDbPath}")
            .Options;
        _localFactory = new TestDbFactory(options);
        await using (var db = await _localFactory.CreateDbContextAsync())
            await db.Database.EnsureCreatedAsync();

        _pendingSecrets = new FakePendingSecrets();
        _offlineReg = new OfflineRegistrationStore(_localFactory);

        var http = _factory.CreateClient();
        var apiOptions = Options.Create(new ApiSettings
        {
            BaseUrl = http.BaseAddress!.ToString(),
            TimeoutSeconds = 60
        });
        _authApi = new AuthApiClient(new ApiClient(http, apiOptions));

        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        _email = $"offline.auth.test.{stamp}@example.local";
        _password = "OfflineGate1!";
        _companyName = $"Offline Auth Test {stamp}";
        _installationId = Guid.NewGuid();
        _deviceId = $"device-gate-{stamp}";
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        try
        {
            if (File.Exists(_localDbPath))
                File.Delete(_localDbPath);
        }
        catch
        {
            // best-effort
        }
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CompleteChain_OfflineCreate_Sync_OnlineLogin_Idempotent()
    {
        // TEST 3–5: Offline create → Local SQLite → SyncQueue Pending
        var request = new RegisterRequest
        {
            FullName = "Offline Gate Owner",
            Email = _email,
            MobileNumber = UniqueMobile(),
            Password = _password,
            ConfirmPassword = _password,
            CompanyName = _companyName,
            OwnerName = "Offline Gate Owner",
            CompanyEmail = $"co.{_email}",
            CompanyMobile = UniqueMobile(),
            Plan = "Starter",
            InstallationId = _installationId,
            DeviceId = _deviceId,
            DeviceName = "Gate Device",
            Platform = "Tests",
            ClientUserId = Guid.NewGuid(),
            ClientCompanyId = Guid.NewGuid(),
            ClientUserCompanyId = Guid.NewGuid(),
            ClientSubscriptionId = Guid.NewGuid()
        };

        var ids = await _offlineReg.SaveOfflineRegistrationAsync(request, _installationId);
        _userId = ids.UserId;
        _companyId = ids.CompanyId;
        _userCompanyId = ids.UserCompanyId;
        _subscriptionId = ids.SubscriptionId;
        await _pendingSecrets.SetPendingPasswordAsync(_userId, _password);

        await using (var local = await _localFactory.CreateDbContextAsync())
        {
            Assert.Equal(_email, (await local.Users.SingleAsync()).Email);
            Assert.Equal(_companyName, (await local.Companies.SingleAsync()).CompanyName);
            Assert.Equal(_userCompanyId, (await local.UserCompanies.SingleAsync()).Id);
            Assert.Equal(_subscriptionId, (await local.Subscriptions.SingleAsync()).Id);
            var queue = await local.SyncQueue.OrderBy(q => q.EntityType).ToListAsync();
            Assert.Equal(4, queue.Count);
            Assert.All(queue, q =>
            {
                Assert.Equal((int)RecordSyncStatus.Pending, q.Status);
                Assert.Equal((int)SyncOperation.Create, q.Operation);
                Assert.False(string.IsNullOrWhiteSpace(q.PayloadJson));
                Assert.DoesNotContain(_password, q.PayloadJson!, StringComparison.Ordinal);
            });
            Assert.Contains(queue, q => q.EntityType == "User");
            Assert.Contains(queue, q => q.EntityType == "Company");
            Assert.Contains(queue, q => q.EntityType == "UserCompany");
            Assert.Contains(queue, q => q.EntityType == "Subscription");

            var conn = local.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT IFNULL(group_concat(Email||'|'||FullName||'|'||MobileNumber), '') FROM LocalUsers
                UNION ALL
                SELECT IFNULL(group_concat(CompanyName||'|'||Email||'|'||MobileNumber), '') FROM LocalCompanies
                UNION ALL
                SELECT IFNULL(group_concat(PayloadJson), '') FROM LocalSyncQueue
                """;
            var parts = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                parts.Add(reader.IsDBNull(0) ? "" : reader.GetString(0));
            var localText = string.Join('|', parts);
            Assert.DoesNotContain(_password, localText, StringComparison.Ordinal);
        }

        // TEST 6: Server must NOT have the data yet
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var server = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await server.Users.AnyAsync(u => u.Email == _email && !u.IsDeleted));
            Assert.False(await server.Companies.AnyAsync(c => c.CompanyName == _companyName && !c.IsDeleted));
        }

        // TEST 7–8: Internet ON → Sync API (POST /api/auth/register)
        var connectivity = new FakeConnectivity(online: true);
        var sync = new RegistrationSyncService(
            _localFactory,
            _authApi,
            _pendingSecrets,
            connectivity,
            new AppSessionContext(),
            NullLogger<RegistrationSyncService>.Instance);
        var synced = await sync.SyncNowAsync();
        Assert.True(synced, "Registration sync must succeed against Development API.");

        // TEST 9–13: Server has data, IDs match, queue Synced, no duplicates
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var server = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await server.Users.SingleAsync(u => u.Email == _email && !u.IsDeleted);
            var company = await server.Companies.SingleAsync(c => c.CompanyName == _companyName && !c.IsDeleted);
            var membership = await server.UserCompanies.SingleAsync(uc => uc.UserId == user.Id && !uc.IsDeleted);
            var subscription = await server.Subscriptions.SingleAsync(s => s.CompanyId == company.Id && !s.IsDeleted);

            Assert.Equal(_userId, user.Id);
            Assert.Equal(_companyId, company.Id);
            Assert.Equal(_userCompanyId, membership.Id);
            Assert.Equal(_subscriptionId, subscription.Id);
            Assert.Equal("Offline Gate Owner", user.FullName);
            Assert.Equal(_companyName, company.CompanyName);
            Assert.Equal(1, await server.Users.CountAsync(u => u.Email == _email && !u.IsDeleted));
            Assert.Equal(1, await server.Companies.CountAsync(c => c.CompanyName == _companyName && !c.IsDeleted));
            Assert.Equal(1, await server.UserCompanies.CountAsync(uc => uc.Id == _userCompanyId && !uc.IsDeleted));
            Assert.Equal(1, await server.Subscriptions.CountAsync(s => s.Id == _subscriptionId && !s.IsDeleted));
        }

        await using (var local = await _localFactory.CreateDbContextAsync())
        {
            var queue = await local.SyncQueue.ToListAsync();
            Assert.All(queue, q =>
            {
                Assert.Equal((int)RecordSyncStatus.Synced, q.Status);
                Assert.NotNull(q.SyncedAt);
                Assert.True(string.IsNullOrWhiteSpace(q.Error));
            });
        }

        Assert.Null(await _pendingSecrets.GetPendingPasswordAsync(_userId));

        // TEST 14: Online login after sync
        var login = await _authApi.LoginAsync(new LoginRequest
        {
            Email = _email,
            Password = _password,
            DeviceId = _deviceId,
            DeviceName = "Gate Device",
            Platform = "Tests"
        });
        Assert.True(login.Success, login.Error);
        Assert.NotNull(login.Data);
        Assert.False(string.IsNullOrWhiteSpace(login.Data!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(login.Data.RefreshToken));
        Assert.Equal(_userId, login.Data.User.Id);
        Assert.Equal(_companyId, login.Data.CurrentCompany.Id);
        Assert.Contains(login.Data.Roles, r => r.Contains("Owner", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(login.Data.Permissions);

        // TEST 20: Refresh + rotation
        var refresh1 = await _authApi.RefreshAsync(new RefreshRequest
        {
            RefreshToken = login.Data.RefreshToken,
            SessionId = login.Data.Session.SessionId,
            DeviceId = _deviceId
        });
        Assert.True(refresh1.Success, refresh1.Error);
        Assert.NotEqual(login.Data.RefreshToken, refresh1.Data!.RefreshToken);

        var reuse = await _authApi.RefreshAsync(new RefreshRequest
        {
            RefreshToken = login.Data.RefreshToken,
            SessionId = login.Data.Session.SessionId,
            DeviceId = _deviceId
        });
        Assert.False(reuse.Success);

        // TEST 21: Online logout
        var logout = await _authApi.LogoutAsync(
            new LogoutRequest
            {
                SessionId = refresh1.Data.Session.SessionId,
                RefreshToken = refresh1.Data.RefreshToken
            },
            refresh1.Data.AccessToken);
        Assert.True(logout.Success, logout.Error);

        // TEST 24: Idempotent re-register of same client IDs does not duplicate
        await _pendingSecrets.SetPendingPasswordAsync(_userId, _password);
        await using (var local = await _localFactory.CreateDbContextAsync())
        {
            foreach (var q in local.SyncQueue)
            {
                q.Status = (int)RecordSyncStatus.Pending;
                q.SyncedAt = null;
                q.Error = null;
                q.RetryCount = 0;
            }
            await local.SaveChangesAsync();
        }
        Assert.True(await sync.SyncNowAsync());
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var server = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await server.Users.CountAsync(u => u.Email == _email && !u.IsDeleted));
            Assert.Equal(1, await server.Companies.CountAsync(c => c.CompanyName == _companyName && !c.IsDeleted));
        }
    }

    [Fact]
    public async Task SyncRetry_WhenApiUnavailable_StaysPending_ThenSucceeds()
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var email = $"offline.retry.{stamp}@example.local";
        var company = $"Offline Retry {stamp}";
        var installationId = Guid.NewGuid();
        var request = new RegisterRequest
        {
            FullName = "Retry Owner",
            Email = email,
            MobileNumber = UniqueMobile(),
            Password = "RetryGate1!",
            ConfirmPassword = "RetryGate1!",
            CompanyName = company,
            CompanyEmail = $"co.{email}",
            CompanyMobile = UniqueMobile(),
            Plan = "Starter",
            InstallationId = installationId,
            DeviceId = $"device-retry-{stamp}",
            DeviceName = "Retry Device",
            Platform = "Tests",
            ClientUserId = Guid.NewGuid(),
            ClientCompanyId = Guid.NewGuid(),
            ClientUserCompanyId = Guid.NewGuid(),
            ClientSubscriptionId = Guid.NewGuid()
        };

        var ids = await _offlineReg.SaveOfflineRegistrationAsync(request, installationId);
        await _pendingSecrets.SetPendingPasswordAsync(ids.UserId, "RetryGate1!");

        var connectivity = new FakeConnectivity(online: true);
        var blocked = new BlockingAuthApi(_authApi) { Block = true };
        var retrySync = new RegistrationSyncService(
            _localFactory,
            blocked,
            _pendingSecrets,
            connectivity,
            new AppSessionContext(),
            NullLogger<RegistrationSyncService>.Instance);

        Assert.False(await retrySync.SyncNowAsync());

        await using (var local = await _localFactory.CreateDbContextAsync())
        {
            Assert.All(await local.SyncQueue.Where(q => q.CompanyId == ids.CompanyId).ToListAsync(),
                q => Assert.NotEqual((int)RecordSyncStatus.Synced, q.Status));
        }

        blocked.Block = false;
        Assert.True(await retrySync.SyncNowAsync());

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var server = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await server.Users.CountAsync(u => u.Email == email && !u.IsDeleted));
        }

        await using (var local = await _localFactory.CreateDbContextAsync())
        {
            Assert.All(await local.SyncQueue.Where(q => q.CompanyId == ids.CompanyId).ToListAsync(),
                q => Assert.Equal((int)RecordSyncStatus.Synced, q.Status));
        }
    }

    private static string UniqueMobile()
    {
        var n = Random.Shared.NextInt64(6_000_000_000L, 9_999_999_999L);
        return n.ToString();
    }

    private sealed class TestDbFactory(DbContextOptions<LocalAppDbContext> options)
        : IDbContextFactory<LocalAppDbContext>
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

    private sealed class FakePendingSecrets : IPendingRegistrationSecretStore
    {
        private readonly Dictionary<Guid, string> _bag = new();
        public Task SetPendingPasswordAsync(Guid registrationUserId, string password)
        {
            _bag[registrationUserId] = password;
            return Task.CompletedTask;
        }
        public Task<string?> GetPendingPasswordAsync(Guid registrationUserId)
            => Task.FromResult(_bag.TryGetValue(registrationUserId, out var p) ? p : null);
        public Task ClearPendingPasswordAsync(Guid registrationUserId)
        {
            _bag.Remove(registrationUserId);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingAuthApi(IAuthApiClient inner) : IAuthApiClient
    {
        public bool Block { get; set; }

        public Task<ApiCallResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
            => Block
                ? Task.FromResult(ApiCallResult<LoginResponse>.Fail(0, "Unable to connect to the server. Please try again.", network: true))
                : inner.LoginAsync(request, cancellationToken);

        public Task<ApiCallResult<LoginResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
            => inner.RefreshAsync(request, cancellationToken);

        public Task<ApiCallResult> LogoutAsync(LogoutRequest request, string accessToken, CancellationToken cancellationToken = default)
            => inner.LogoutAsync(request, accessToken, cancellationToken);

        public Task<ApiCallResult<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
            => Block
                ? Task.FromResult(ApiCallResult<RegisterResponse>.Fail(0, "Unable to connect to the server. Please try again.", network: true))
                : inner.RegisterAsync(request, cancellationToken);

        public Task<ApiCallResult<MeResponse>> MeAsync(string accessToken, CancellationToken cancellationToken = default)
            => inner.MeAsync(accessToken, cancellationToken);
    }
}
