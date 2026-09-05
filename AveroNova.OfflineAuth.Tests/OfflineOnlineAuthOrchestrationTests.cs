using AveroNova.Application.DTOs.Auth;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using AveroNova.App.UI.Services.Api;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Security;
using AveroNova.Shared.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

/// <summary>
/// Online/offline auth orchestration against local SQLite + fakes.
/// Live Development API coverage lives in AveroNova.API.Tests + live HTTPS E2E.
/// </summary>
public sealed class OfflineOnlineAuthOrchestrationTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private IDbContextFactory<LocalAppDbContext> _dbFactory = null!;
    private FakeConnectivity _connectivity = null!;
    private FakeTokenStore _tokens = null!;
    private FakeAuthApi _api = null!;
    private InstallationService _installation = null!;
    private LocalAuthSessionStore _sessions = null!;
    private AppSessionContext _context = null!;
    private FakePendingSecrets _pendingSecrets = null!;
    private FakeLocalCredentialStore _credentials = null!;
    private AuthenticationService _auth = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"averonova-auth-test-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LocalAppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _dbFactory = new TestDbContextFactory(options);
        await using (var db = await _dbFactory.CreateDbContextAsync())
            await db.Database.EnsureCreatedAsync();

        _connectivity = new FakeConnectivity(online: true);
        _tokens = new FakeTokenStore();
        _api = new FakeAuthApi();
        _installation = new InstallationService(_dbFactory, NullLogger<InstallationService>.Instance);
        _sessions = new LocalAuthSessionStore(_dbFactory);
        _context = new AppSessionContext();
        var offlineReg = new OfflineRegistrationStore(_dbFactory);
        _pendingSecrets = new FakePendingSecrets();
        _credentials = new FakeLocalCredentialStore();
        _auth = new AuthenticationService(
            _api,
            _tokens,
            _sessions,
            _context,
            _connectivity,
            _installation,
            new FakeDeviceInfo(),
            offlineReg,
            _pendingSecrets,
            _credentials,
            NullLogger<AuthenticationService>.Instance);

        await _installation.EnsureInitializedAsync();
    }

    public Task DisposeAsync()
    {
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task FreshInstallation_IsNotRegistered_CanCreateAccount()
    {
        Assert.Equal(LocalInstallationStatus.NotRegistered, _installation.Status);
        Assert.True(_installation.CanCreateAccount);
        Assert.False(await _auth.TryAutoLoginAsync());
    }

    [Fact]
    public async Task OnlineRegistration_MarksInstallationRegistered_WithoutHidingCreateAccount()
    {
        _api.RegisterResult = ApiCallResult<RegisterResponse>.Ok(CreateRegisterResponse(), 200);
        var (ok, err) = await _auth.RegisterAsync(CreateRegisterRequest());
        Assert.True(ok, err);
        Assert.True(_installation.IsRegistered);
        Assert.True(_installation.CanCreateAccount);
    }

    [Fact]
    public async Task OfflineRegistration_SavesLocally_AndQueuesPending_WithoutApiCall()
    {
        _connectivity.SetOnline(false);
        var request = CreateRegisterRequest();
        request.Email = "offline.owner@test.local";
        request.CompanyEmail = "offline.co@test.local";
        request.MobileNumber = "9111111111";
        request.CompanyMobile = "9111111112";

        var (ok, err) = await _auth.RegisterAsync(request);
        Assert.True(ok, err);
        Assert.True(_installation.IsRegistered);
        Assert.Equal(0, _api.RegisterCalls);

        await using var db = await _dbFactory.CreateDbContextAsync();
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(1, await db.Companies.CountAsync());
        Assert.Equal(1, await db.UserCompanies.CountAsync());
        Assert.Equal(1, await db.Subscriptions.CountAsync());
        Assert.Equal(4, await db.SyncQueue.CountAsync());
        Assert.All(await db.SyncQueue.ToListAsync(), q =>
            Assert.Equal((int)AveroNova.Domain.Enums.RecordSyncStatus.Pending, q.Status));

        var dump = await DumpSqliteTextAsync();
        Assert.DoesNotContain("Password1!", dump);
        Assert.DoesNotContain("PasswordHash", dump);
    }

    [Fact]
    public async Task ApiUnavailable_Registration_FallsBackToLocalSqlite()
    {
        _api.RegisterResult = ApiCallResult<RegisterResponse>.Fail(
            0, "Unable to connect to the server. Please try again.", network: true);

        var request = CreateRegisterRequest();
        var (ok, err) = await _auth.RegisterAsync(request);

        Assert.True(ok, err);
        Assert.True(_installation.IsRegistered);
        Assert.Equal(1, _api.RegisterCalls);
        await using var db = await _dbFactory.CreateDbContextAsync();
        Assert.Single(await db.Users.ToListAsync());
        Assert.Equal(4, await db.SyncQueue.CountAsync());
    }

    [Fact]
    public async Task OnlineLogin_StoresTokensSecurely_AndLocalSessionInSqlite()
    {
        await SeedRegisteredInstallationAsync();
        var login = CreateLoginResponse("owner@test.local");
        _api.LoginResult = ApiCallResult<LoginResponse>.Ok(login, 200);

        var (ok, err) = await _auth.LoginAsync("owner@test.local", "Password1!");
        Assert.True(ok, err);
        Assert.Equal(login.AccessToken, await _tokens.GetAccessTokenAsync());
        Assert.Equal(login.RefreshToken, await _tokens.GetRefreshTokenAsync());
        Assert.DoesNotContain(login.AccessToken, await DumpSqliteTextAsync());
        Assert.DoesNotContain(login.RefreshToken, await DumpSqliteTextAsync());
        Assert.DoesNotContain("Password1!", await DumpSqliteTextAsync());

        var snapshot = await _sessions.LoadValidSessionAsync(_installation.InstallationId);
        Assert.NotNull(snapshot);
        Assert.Equal(login.User.Id, snapshot!.User.Id);
        Assert.Contains("Company.Owner", snapshot.Roles);
        Assert.NotEmpty(snapshot.Permissions);
    }

    [Fact]
    public async Task OfflineLogin_WithLocalPassword_DoesNotCallApi()
    {
        await SeedRegisteredInstallationAsync();
        await SeedLocalSessionFromLoginAsync(CreateLoginResponse("owner@test.local"));
        await SeedLocalPasswordAsync("owner@test.local", "Password1!");
        _connectivity.SetOnline(false);
        _api.LoginCalls = 0;

        var (ok, err) = await _auth.LoginAsync("owner@test.local", "Password1!");
        Assert.True(ok, err);
        Assert.Equal(0, _api.LoginCalls);
        Assert.True(_auth.IsAuthenticated);
    }

    [Fact]
    public async Task ApiUnavailable_Login_FallsBackToLocalPassword()
    {
        await SeedRegisteredInstallationAsync();
        await SeedLocalSessionFromLoginAsync(CreateLoginResponse("owner@test.local"));
        await SeedLocalPasswordAsync("owner@test.local", "Password1!");
        _api.LoginResult = ApiCallResult<LoginResponse>.Fail(
            0, "Unable to connect to the server. Please try again.", network: true);

        var (ok, err) = await _auth.LoginAsync("owner@test.local", "Password1!");

        Assert.True(ok, err);
        Assert.Equal(1, _api.LoginCalls);
        Assert.True(_auth.IsAuthenticated);
    }

    [Fact]
    public async Task ApiUnavailable_LoginWithoutLocalAccount_ShowsOfflineRecoveryMessage()
    {
        _api.LoginResult = ApiCallResult<LoginResponse>.Fail(
            0, "Unable to connect to the server. Please try again.", network: true);

        var (ok, error) = await _auth.LoginAsync("missing@test.local", "Password1!");

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.DoesNotContain("connect to the server", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No local account", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Create an account", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OfflineResetPassword_ChangesLocalCredential_AndRequiresNewPassword()
    {
        _connectivity.SetOnline(false);
        var request = CreateRegisterRequest();
        Assert.True((await _auth.RegisterAsync(request)).Success);

        var (resetOk, resetError) = await _auth.ResetPasswordAsync(request.Email, "NewPassword1!");
        Assert.True(resetOk, resetError);
        Assert.Equal("NewPassword1!", await _pendingSecrets.GetPendingPasswordAsync(request.ClientUserId!.Value));

        var oldLogin = await _auth.LoginAsync(request.Email, request.Password);
        Assert.False(oldLogin.Success);

        var newLogin = await _auth.LoginAsync(request.Email, "NewPassword1!");
        Assert.True(newLogin.Success, newLogin.Error);
        Assert.True(_auth.IsAuthenticated);
    }

    [Fact]
    public async Task OfflineResetPassword_RejectsUnknownLocalAccount()
    {
        var (ok, error) = await _auth.ResetPasswordAsync("missing@test.local", "NewPassword1!");

        Assert.False(ok);
        Assert.Equal("No local account was found for this email.", error);
    }

    [Fact]
    public async Task OfflineLogin_AfterOfflineRegister_WorksWithoutInternet()
    {
        _connectivity.SetOnline(false);
        var request = CreateRegisterRequest();
        var (regOk, regErr) = await _auth.RegisterAsync(request);
        Assert.True(regOk, regErr);

        var (ok, err) = await _auth.LoginAsync(request.Email, request.Password);
        Assert.True(ok, err);
        Assert.Equal(0, _api.LoginCalls);
        Assert.True(_auth.IsAuthenticated);
        Assert.NotEmpty(_context.Permissions);
        Assert.True(_context.HasPermission("Dashboard.View"));
    }

    [Fact]
    public async Task OfflineLogin_WrongPassword_Fails()
    {
        _connectivity.SetOnline(false);
        var request = CreateRegisterRequest();
        Assert.True((await _auth.RegisterAsync(request)).Success);
        var (ok, err) = await _auth.LoginAsync(request.Email, "WrongPass1!");
        Assert.False(ok);
        Assert.Equal("Invalid email or password.", err);
    }

    [Fact]
    public async Task OfflineLogin_ExpiredSession_SucceedsWithLocalPassword()
    {
        await SeedRegisteredInstallationAsync();
        var login = CreateLoginResponse("owner@test.local");
        await _sessions.SaveFromLoginAsync(login, _installation.InstallationId);
        await SeedLocalPasswordAsync("owner@test.local", "Password1!");
        await using (var db = await _dbFactory.CreateDbContextAsync())
        {
            var session = await db.Sessions.SingleAsync();
            session.OfflineExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        _connectivity.SetOnline(false);
        var (ok, err) = await _auth.LoginAsync("owner@test.local", "Password1!");
        Assert.True(ok, err);
        Assert.True(_auth.IsAuthenticated);
        var snapshot = await _sessions.LoadValidSessionAsync(_installation.InstallationId);
        Assert.NotNull(snapshot);
        Assert.True(snapshot!.Session.OfflineExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task TryAutoLogin_Offline_WithValidSession_SucceedsWithoutApi()
    {
        await SeedRegisteredInstallationAsync();
        await SeedLocalSessionFromLoginAsync(CreateLoginResponse("owner@test.local"));
        _connectivity.SetOnline(false);
        _api.RefreshCalls = 0;

        Assert.True(await _auth.TryAutoLoginAsync());
        Assert.True(_auth.IsAuthenticated);
        Assert.Equal(0, _api.RefreshCalls);
        Assert.Equal(0, _api.LoginCalls);
    }

    [Fact]
    public async Task OfflineLogout_ClearsAuth_KeepsBusinessSyncQueue()
    {
        await SeedRegisteredInstallationAsync();
        var login = CreateLoginResponse("owner@test.local");
        await SeedLocalSessionFromLoginAsync(login);
        await _tokens.SetAccessTokenAsync(login.AccessToken, login.AccessTokenExpiresAtUtc);
        await _tokens.SetRefreshTokenAsync(login.RefreshToken);
        await using (var db = await _dbFactory.CreateDbContextAsync())
        {
            db.SyncQueue.Add(new LocalSyncQueueEntity
            {
                Id = Guid.NewGuid(),
                EntityType = "Product",
                EntityId = Guid.NewGuid(),
                Operation = 1,
                Status = 0,
                CreatedAt = DateTime.UtcNow,
                CompanyId = login.CurrentCompany.Id
            });
            await db.SaveChangesAsync();
        }

        _connectivity.SetOnline(false);
        await _auth.LogoutAsync();

        Assert.False(_auth.IsAuthenticated);
        Assert.Null(await _tokens.GetAccessTokenAsync());
        Assert.Null(await _sessions.LoadValidSessionAsync(_installation.InstallationId));
        Assert.Equal(0, _api.LogoutCalls);

        await using (var db = await _dbFactory.CreateDbContextAsync())
        {
            Assert.Equal(1, await db.SyncQueue.CountAsync());
            Assert.Equal(1, await db.Users.CountAsync());
            Assert.True(_installation.IsRegistered);
        }
    }

    [Fact]
    public async Task OfflineLogin_AfterLogout_WorksWithLocalPassword()
    {
        await SeedRegisteredInstallationAsync();
        var login = CreateLoginResponse("owner@test.local");
        await SeedLocalSessionFromLoginAsync(login);
        await SeedLocalPasswordAsync("owner@test.local", "Password1!");
        _connectivity.SetOnline(false);
        await _auth.LogoutAsync();

        var (ok, err) = await _auth.LoginAsync("owner@test.local", "Password1!");
        Assert.True(ok, err);
        Assert.Equal(0, _api.LoginCalls);
        Assert.True(_auth.IsAuthenticated);
    }

    [Fact]
    public async Task OnlineRestored_Login_CallsApiAndRestoresSession()
    {
        await SeedRegisteredInstallationAsync();
        _connectivity.SetOnline(false);
        await _auth.LogoutAsync();
        _connectivity.SetOnline(true);
        var login = CreateLoginResponse("owner@test.local");
        _api.LoginResult = ApiCallResult<LoginResponse>.Ok(login, 200);

        var (ok, err) = await _auth.LoginAsync("owner@test.local", "Password1!");
        Assert.True(ok, err);
        Assert.Equal(1, _api.LoginCalls);
        Assert.True(_auth.IsAuthenticated);
    }

    [Fact]
    public async Task OnlineRefresh_RotatesTokensViaApi()
    {
        await SeedRegisteredInstallationAsync();
        var login = CreateLoginResponse("owner@test.local");
        await SeedLocalSessionFromLoginAsync(login);
        await _tokens.SetAccessTokenAsync(login.AccessToken, DateTime.UtcNow.AddMinutes(-1));
        await _tokens.SetRefreshTokenAsync(login.RefreshToken);
        await _tokens.SetSessionIdAsync(login.Session.SessionId);

        var refreshed = CreateLoginResponse("owner@test.local");
        refreshed.AccessToken = "new-access";
        refreshed.RefreshToken = "new-refresh";
        _api.RefreshResult = ApiCallResult<LoginResponse>.Ok(refreshed, 200);

        var (ok, err) = await _auth.RefreshTokenAsync();
        Assert.True(ok, err);
        Assert.Equal(1, _api.RefreshCalls);
        Assert.Equal("new-access", await _tokens.GetAccessTokenAsync());
        Assert.Equal("new-refresh", await _tokens.GetRefreshTokenAsync());
    }

    [Fact]
    public async Task OnlineLogout_CallsApiAndClearsLocalAuth()
    {
        await SeedRegisteredInstallationAsync();
        var login = CreateLoginResponse("owner@test.local");
        await SeedLocalSessionFromLoginAsync(login);
        await _tokens.SetAccessTokenAsync(login.AccessToken, login.AccessTokenExpiresAtUtc);
        await _tokens.SetRefreshTokenAsync(login.RefreshToken);
        await _tokens.SetSessionIdAsync(login.Session.SessionId);
        _api.LogoutResult = ApiCallResult.Ok(200);

        await _auth.LogoutAsync();
        Assert.Equal(1, _api.LogoutCalls);
        Assert.Null(await _tokens.GetAccessTokenAsync());
        Assert.False(_auth.IsAuthenticated);
    }

    [Fact]
    public async Task ApiUnavailable_DoesNotCrash_ReturnsFriendlyError()
    {
        await SeedRegisteredInstallationAsync();
        _api.LoginResult = ApiCallResult<LoginResponse>.Fail(0, "Unable to connect to the server. Please try again.", network: true);
        var (ok, err) = await _auth.LoginAsync("owner@test.local", "Password1!");
        Assert.False(ok);
        Assert.Contains("Unable to connect", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApiUnavailable_WithValidLocalSession_AutoLoginContinues()
    {
        await SeedRegisteredInstallationAsync();
        await SeedLocalSessionFromLoginAsync(CreateLoginResponse("owner@test.local"));
        await _tokens.SetAccessTokenAsync("old", DateTime.UtcNow.AddMinutes(-5));
        await _tokens.SetRefreshTokenAsync("refresh");
        _api.RefreshResult = ApiCallResult<LoginResponse>.Fail(0, "Unable to connect to the server. Please try again.", network: true);

        Assert.True(await _auth.TryAutoLoginAsync());
        Assert.True(_auth.IsAuthenticated);
    }

    [Fact]
    public async Task SyncContext_UsesAuthenticatedSession_NotDuplicateAuth()
    {
        await SeedRegisteredInstallationAsync();
        await SeedLocalSessionFromLoginAsync(CreateLoginResponse("owner@test.local"));
        Assert.True(await _auth.TryAutoLoginAsync());
        Assert.True(_context.IsAuthenticated);
        Assert.NotNull(_context.CurrentUserId);
        Assert.NotNull(_context.CurrentCompanyId);
        Assert.NotNull(_context.ServerSessionId);
        Assert.NotEmpty(_context.Permissions);
    }

    [Fact]
    public async Task Sqlite_Schema_HasNoTokenOrPasswordColumns()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name || ':' || IFNULL(sql,'') FROM sqlite_master WHERE type='table'";
        var ddl = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ddl.Add(reader.GetString(0));

        var joined = string.Join('\n', ddl);
        Assert.DoesNotContain("AccessToken", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RefreshToken", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SigningKey", joined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LocalSessions", joined);
        Assert.Contains("LocalInstallations", joined);
        Assert.Contains("LocalCustomers", joined);
        Assert.Contains("LocalProducts", joined);
        Assert.Contains("LocalInvoices", joined);
        Assert.Contains("LocalPayments", joined);
        Assert.Contains("LocalLicenses", joined);
    }

    private async Task SeedRegisteredInstallationAsync()
        => await _installation.MarkRegisteredAsync(Guid.NewGuid(), Guid.NewGuid());

    private async Task SeedLocalSessionFromLoginAsync(LoginResponse login)
    {
        await _sessions.SaveFromLoginAsync(login, _installation.InstallationId);
        _context.SetFromLogin(login);
    }

    private async Task SeedLocalPasswordAsync(string email, string password)
    {
        var user = await _sessions.FindUserByEmailAsync(email);
        Assert.NotNull(user);
        var hasher = new Pbkdf2PasswordHasher();
        await _credentials.SetPasswordHashAsync(user!.Id, email, hasher.HashPassword(password));
    }

    private async Task<string> DumpSqliteTextAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT IFNULL(group_concat(Email||'|'||FullName||'|'||MobileNumber), '') FROM LocalUsers
            UNION ALL
            SELECT IFNULL(group_concat(CompanyName||'|'||Email||'|'||MobileNumber), '') FROM LocalCompanies
            UNION ALL
            SELECT IFNULL(group_concat(cast(ServerSessionId as text)||'|'||DeviceId), '') FROM LocalSessions
            UNION ALL
            SELECT IFNULL(group_concat(RoleName), '') FROM LocalRoles
            UNION ALL
            SELECT IFNULL(group_concat(PermissionName), '') FROM LocalPermissions
            """;
        var parts = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            parts.Add(reader.IsDBNull(0) ? "" : reader.GetString(0));
        return string.Join('|', parts);
    }

    private static RegisterRequest CreateRegisterRequest() => new()
    {
        FullName = "Test Owner",
        Email = "owner@test.local",
        MobileNumber = "9123456789",
        Password = "Password1!",
        ConfirmPassword = "Password1!",
        CompanyName = "Test Co",
        CompanyEmail = "co@test.local",
        CompanyMobile = "9123456780",
        Plan = "Starter"
    };

    private static RegisterResponse CreateRegisterResponse() => new()
    {
        Success = true,
        UserId = Guid.NewGuid(),
        CompanyId = Guid.NewGuid(),
        SubscriptionId = Guid.NewGuid(),
        Plan = "Starter",
        TrialStartDate = DateTime.UtcNow,
        TrialEndDate = DateTime.UtcNow.AddDays(15)
    };

    private static LoginResponse CreateLoginResponse(string email)
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        return new LoginResponse
        {
            AccessToken = $"access-{Guid.NewGuid():N}",
            RefreshToken = $"refresh-{Guid.NewGuid():N}",
            ExpiresIn = 900,
            AccessTokenExpiresAtUtc = now.AddMinutes(15),
            User = new AuthUserDto
            {
                Id = userId,
                FullName = "Test Owner",
                Email = email,
                MobileNumber = "9123456789",
                IsActive = true
            },
            CurrentCompany = new AuthCompanyDto
            {
                Id = companyId,
                CompanyName = "Test Co",
                Email = "co@test.local",
                MobileNumber = "9123456780",
                IsDefault = true,
                IsOwner = true
            },
            Companies =
            [
                new AuthCompanyDto
                {
                    Id = companyId,
                    CompanyName = "Test Co",
                    Email = "co@test.local",
                    MobileNumber = "9123456780",
                    IsDefault = true,
                    IsOwner = true
                }
            ],
            Roles = ["Company.Owner"],
            Permissions = ["users.manage", "products.view"],
            Session = new AuthSessionDto
            {
                SessionId = sessionId,
                UserId = userId,
                CompanyId = companyId,
                DeviceId = "device-1",
                DeviceName = "Test",
                Platform = "Tests",
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddDays(14),
                OfflineSessionExpiresAtUtc = now.Add(OfflineSessionDefaults.OfflineSessionMaxAge)
            }
        };
    }

    private sealed class TestDbContextFactory(DbContextOptions<LocalAppDbContext> options)
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

    private sealed class FakeTokenStore : ISecureTokenStore
    {
        private readonly Dictionary<string, string> _bag = new(StringComparer.Ordinal);
        public Task SetAccessTokenAsync(string token, DateTime expiresUtc)
        {
            _bag["a"] = token;
            _bag["e"] = expiresUtc.ToUniversalTime().ToString("O");
            return Task.CompletedTask;
        }
        public Task SetRefreshTokenAsync(string token) { _bag["r"] = token; return Task.CompletedTask; }
        public Task SetSessionIdAsync(Guid sessionId) { _bag["s"] = sessionId.ToString("D"); return Task.CompletedTask; }
        public Task<string?> GetAccessTokenAsync() => Task.FromResult(_bag.TryGetValue("a", out var v) ? v : null);
        public Task<string?> GetRefreshTokenAsync() => Task.FromResult(_bag.TryGetValue("r", out var v) ? v : null);
        public Task<DateTime?> GetAccessTokenExpiryAsync()
            => Task.FromResult(_bag.TryGetValue("e", out var raw) && DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
                ? dt.ToUniversalTime() : (DateTime?)null);
        public Task<Guid?> GetSessionIdAsync()
            => Task.FromResult(_bag.TryGetValue("s", out var raw) && Guid.TryParse(raw, out var id) ? id : (Guid?)null);
        public Task ClearAsync() { _bag.Clear(); return Task.CompletedTask; }
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

    private sealed class FakeLocalCredentialStore : ILocalCredentialStore
    {
        private readonly Dictionary<Guid, string> _hashes = new();
        private readonly Dictionary<string, Guid> _emails = new(StringComparer.OrdinalIgnoreCase);

        public Task SetPasswordHashAsync(Guid userId, string email, string passwordHash)
        {
            _hashes[userId] = passwordHash;
            _emails[email.Trim()] = userId;
            return Task.CompletedTask;
        }

        public Task<string?> GetPasswordHashAsync(Guid userId)
            => Task.FromResult(_hashes.TryGetValue(userId, out var hash) ? hash : null);

        public Task<Guid?> FindUserIdByEmailAsync(string email)
            => Task.FromResult(_emails.TryGetValue(email.Trim(), out var id) ? id : (Guid?)null);
    }

    private sealed class FakeDeviceInfo : IClientDeviceInfo
    {
        public string Name => "Test Device";
        public string Platform => "Tests";
    }

    private sealed class FakeAuthApi : IAuthApiClient
    {
        public int LoginCalls;
        public int RegisterCalls;
        public int RefreshCalls;
        public int LogoutCalls;
        public ApiCallResult<LoginResponse> LoginResult { get; set; } =
            ApiCallResult<LoginResponse>.Fail(401, "Invalid email or password.");
        public ApiCallResult<RegisterResponse> RegisterResult { get; set; } =
            ApiCallResult<RegisterResponse>.Fail(400, "bad");
        public ApiCallResult<LoginResponse> RefreshResult { get; set; } =
            ApiCallResult<LoginResponse>.Fail(401, "invalid");
        public ApiCallResult LogoutResult { get; set; } = ApiCallResult.Ok(200);

        public Task<ApiCallResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        { LoginCalls++; return Task.FromResult(LoginResult); }
        public Task<ApiCallResult<LoginResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
        { RefreshCalls++; return Task.FromResult(RefreshResult); }
        public Task<ApiCallResult> LogoutAsync(LogoutRequest request, string accessToken, CancellationToken cancellationToken = default)
        { LogoutCalls++; return Task.FromResult(LogoutResult); }
        public Task<ApiCallResult<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
        { RegisterCalls++; return Task.FromResult(RegisterResult); }
        public Task<ApiCallResult<MeResponse>> MeAsync(string accessToken, CancellationToken cancellationToken = default)
            => Task.FromResult(ApiCallResult<MeResponse>.Fail(401, "Unauthorized"));
    }
}
