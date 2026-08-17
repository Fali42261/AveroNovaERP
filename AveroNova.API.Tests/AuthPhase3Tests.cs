using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AveroNova.Application.DTOs.Auth;
using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AveroNova.API.Tests;

public sealed class AuthPhase3Tests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly AuthWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public AuthPhase3Tests(AuthWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_CreatesUserCompanySubscriptionRole_HashedPassword_15DayTrial()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("dbcheck");
        var password = "Password1!";
        var reg = await RegisterAsync(client, email);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.SingleAsync(u => u.Id == reg.UserId);
        Assert.Equal(email.ToLowerInvariant(), user.Email);
        Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash));
        Assert.DoesNotContain(password, user.PasswordHash);
        Assert.NotEqual(password, user.PasswordHash);

        var company = await db.Companies.SingleAsync(c => c.Id == reg.CompanyId);
        Assert.False(string.IsNullOrWhiteSpace(company.CompanyName));

        Assert.Equal(1, await db.UserCompanies.CountAsync(uc => uc.UserId == user.Id && uc.CompanyId == company.Id));

        var sub = await db.Subscriptions.Include(s => s.Plan).SingleAsync(s => s.CompanyId == company.Id);
        Assert.Equal(PlanNames.Starter, sub.Plan.Name);
        Assert.True(sub.IsTrial);
        Assert.Equal(SubscriptionStatus.Active, sub.Status);
        Assert.Equal(15, (sub.EndDate.Date - sub.StartDate.Date).TotalDays, 0);

        Assert.True(await db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.CompanyId == company.Id));
        var roleIds = await db.UserRoles.Where(ur => ur.UserId == user.Id).Select(ur => ur.RoleId).ToListAsync();
        Assert.True(await db.RolePermissions.AnyAsync(rp => roleIds.Contains(rp.RoleId)));

        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == user.Email && !u.IsDeleted));
        Assert.Equal(1, await db.ClientInstallations.CountAsync(i => i.UserId == user.Id));
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokensAndOfflineContract()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("login");
        await RegisterAsync(client, email);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password1!",
            DeviceId = "device-1",
            DeviceName = "Test",
            Platform = "Tests"
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<ApiEnvelope<LoginResponse>>(_json);
        Assert.NotNull(body?.Data);
        Assert.False(string.IsNullOrWhiteSpace(body!.Data!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.Data.RefreshToken));
        Assert.True(body.Data.ExpiresIn > 0 && body.Data.ExpiresIn <= 3600);
        Assert.NotEqual(Guid.Empty, body.Data.User.Id);
        Assert.NotEqual(Guid.Empty, body.Data.CurrentCompany.Id);
        Assert.NotEmpty(body.Data.Companies);
        Assert.NotEmpty(body.Data.Roles);
        Assert.NotEmpty(body.Data.Permissions);
        Assert.NotEqual(Guid.Empty, body.Data.Session.SessionId);
        Assert.True(body.Data.Session.OfflineSessionExpiresAtUtc > body.Data.Session.CreatedAtUtc);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsGeneric401()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("badpass");
        await RegisterAsync(client, email);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "WrongPassword!",
            DeviceId = "device-1"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        var text = await login.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password.", text);
        Assert.DoesNotContain("inactive", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("does not exist", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsGeneric401()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = UniqueEmail("unknown"),
            Password = "Password1!",
            DeviceId = "device-1"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        var text = await login.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password.", text);
    }

    [Fact]
    public async Task Login_InactiveUser_ReturnsGeneric401()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("inactive");
        var reg = await RegisterAsync(client, email);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == reg.UserId);
            user.IsActiveUser = false;
            await db.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password1!",
            DeviceId = "device-1"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Contains("Invalid email or password.", await login.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_UnauthorizedCompany_Returns403()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("co");
        await RegisterAsync(client, email);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password1!",
            CompanyId = Guid.NewGuid(),
            DeviceId = "device-1"
        });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task Login_MultiCompany_SelectsMembershipCompany()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("multi");
        var reg = await RegisterAsync(client, email);

        Guid secondCompanyId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var company = new Company
            {
                Id = Guid.NewGuid(),
                CompanyCode = "C2",
                CompanyName = "Second Co",
                Email = UniqueEmail("c2"),
                MobileNumber = "9999999999",
                IsActive = true
            };
            db.Companies.Add(company);
            db.UserCompanies.Add(new UserCompany
            {
                Id = Guid.NewGuid(),
                UserId = reg.UserId,
                CompanyId = company.Id,
                IsActive = true,
                IsDefault = false,
                IsOwner = false
            });
            await db.SaveChangesAsync();
            secondCompanyId = company.Id;
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password1!",
            CompanyId = secondCompanyId,
            DeviceId = "device-multi"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<ApiEnvelope<LoginResponse>>(_json);
        Assert.Equal(secondCompanyId, body!.Data!.CurrentCompany.Id);
        Assert.True(body.Data.Companies.Count >= 2);
    }

    [Fact]
    public async Task Refresh_RotatesAndRejectsReuse()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("refresh");
        await RegisterAsync(client, email);
        var loginBody = await LoginAsync(client, email);
        var oldRefresh = loginBody.RefreshToken;

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest
        {
            RefreshToken = oldRefresh,
            SessionId = loginBody.Session.SessionId,
            DeviceId = "device-1"
        });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = (await refresh.Content.ReadFromJsonAsync<ApiEnvelope<LoginResponse>>(_json))!.Data!;
        Assert.NotEqual(oldRefresh, refreshed.RefreshToken);

        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest
        {
            RefreshToken = oldRefresh,
            SessionId = loginBody.Session.SessionId
        });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefresh()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("logout");
        await RegisterAsync(client, email);
        var loginBody = await LoginAsync(client, email);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.AccessToken);
        var logout = await client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest
        {
            SessionId = loginBody.Session.SessionId
        });
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest
        {
            RefreshToken = loginBody.RefreshToken,
            SessionId = loginBody.Session.SessionId
        });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Me_Authenticated_And_Unauthenticated()
    {
        var client = _factory.CreateClient();
        var unauth = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        var email = UniqueEmail("me");
        await RegisterAsync(client, email);
        var loginBody = await LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.AccessToken);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await me.Content.ReadFromJsonAsync<ApiEnvelope<MeResponse>>(_json);
        Assert.Equal(loginBody.User.Id, body!.Data!.User.Id);
        Assert.Equal(loginBody.CurrentCompany.Id, body.Data.CurrentCompany.Id);
        Assert.NotEmpty(body.Data.Permissions);
    }

    [Fact]
    public async Task Authorization_401_And_403()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/secure/ping")).StatusCode);

        var email = UniqueEmail("authz");
        await RegisterAsync(client, email);
        var loginBody = await LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/secure/ping")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/secure/users-manage")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/secure/impossible")).StatusCode);
    }

    [Fact]
    public async Task Session_Revoked_CannotRefresh()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("revoke");
        await RegisterAsync(client, email);
        var loginBody = await LoginAsync(client, email);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.DeviceSessions.FirstAsync(s => s.Id == loginBody.Session.SessionId);
            session.IsActive = false;
            session.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest
        {
            RefreshToken = loginBody.RefreshToken,
            SessionId = loginBody.Session.SessionId
        });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Register_SameInstallationId_Rejected()
    {
        var client = _factory.CreateClient();
        var installationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid().ToString("N");

        var first = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = "First User",
            Email = UniqueEmail("inst1"),
            MobileNumber = UniqueMobile(),
            Password = "Password1!",
            ConfirmPassword = "Password1!",
            CompanyName = "Co One",
            CompanyEmail = UniqueEmail("c1"),
            CompanyMobile = UniqueMobile(),
            Plan = "Starter",
            InstallationId = installationId,
            DeviceId = deviceId
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = "Second User",
            Email = UniqueEmail("inst2"),
            MobileNumber = UniqueMobile(),
            Password = "Password1!",
            ConfirmPassword = "Password1!",
            CompanyName = "Co Two",
            CompanyEmail = UniqueEmail("c2"),
            CompanyMobile = UniqueMobile(),
            Plan = "Starter",
            InstallationId = installationId,
            DeviceId = deviceId
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var text = await second.Content.ReadAsStringAsync();
        Assert.Contains("already registered", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_MissingInstallationId_Rejected()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = "No Install",
            Email = UniqueEmail("noinst"),
            MobileNumber = UniqueMobile(),
            Password = "Password1!",
            ConfirmPassword = "Password1!",
            CompanyName = "Co",
            CompanyEmail = UniqueEmail("c"),
            CompanyMobile = UniqueMobile(),
            Plan = "Starter",
            DeviceId = "device-x"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<RegisterResponse> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = "Test User",
            Email = email,
            MobileNumber = UniqueMobile(),
            Password = "Password1!",
            ConfirmPassword = "Password1!",
            CompanyName = "Test Co",
            CompanyEmail = email,
            CompanyMobile = UniqueMobile(),
            Plan = "Starter",
            InstallationId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid().ToString("N"),
            DeviceName = "Tests",
            Platform = "Tests"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<RegisterResponse>>(_json);
        Assert.NotNull(body?.Data);
        return body!.Data!;
    }

    private async Task<LoginResponse> LoginAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password1!",
            DeviceId = "device-1",
            DeviceName = "Test",
            Platform = "Tests"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<ApiEnvelope<LoginResponse>>(_json))!.Data!;
    }

    private static string UniqueEmail(string prefix)
        => $"{prefix}.{Guid.NewGuid():N}@example.com";

    private static string UniqueMobile()
        => "9" + Guid.NewGuid().ToString("N")[..9];

    private sealed class ApiEnvelope<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Error { get; set; }
    }
}
