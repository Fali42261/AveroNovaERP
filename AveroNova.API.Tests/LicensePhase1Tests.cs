using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AveroNova.Application.DTOs.Auth;
using AveroNova.Application.DTOs.License;
using AveroNova.Domain.Constants;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AveroNova.API.Tests;

public sealed class LicensePhase1Tests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly AuthWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public LicensePhase1Tests(AuthWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Initialize_Creates15DayStarterTrial_AndDoesNotResetOnRepeat()
    {
        var client = _factory.CreateClient();
        var deviceId = "device-" + Guid.NewGuid().ToString("N");

        var first = await InitializeAsync(client, deviceId);
        Assert.Equal(PlanNames.Starter, first.Plan);
        Assert.True(first.IsTrial);
        Assert.Equal(LicenseStatus.Trial, first.Status);
        Assert.Equal(LicenseConstants.TrialDays, (first.TrialEndDateUtc - first.TrialStartDateUtc).TotalDays, 0);
        Assert.True(first.ServerTimeUtc > DateTime.MinValue);

        var second = await InitializeAsync(client, deviceId);
        Assert.Equal(first.LicenseId, second.LicenseId);
        Assert.Equal(first.TrialStartDateUtc, second.TrialStartDateUtc);
        Assert.Equal(first.TrialEndDateUtc, second.TrialEndDateUtc);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Licenses.CountAsync(l => l.DeviceId == deviceId && !l.IsDeleted));
    }

    [Fact]
    public async Task Initialize_HonorsEarlierOfflineTrialStart_AndDoesNotAcceptFutureStart()
    {
        var client = _factory.CreateClient();
        var deviceId = "device-" + Guid.NewGuid().ToString("N");
        var start = DateTime.UtcNow.AddDays(-5);

        var response = await client.PostAsJsonAsync("/api/license/initialize", new LicenseInitializeRequest
        {
            DeviceId = deviceId,
            ClientLicenseId = Guid.NewGuid(),
            ClientTrialStartDateUtc = start,
            ClientTrialEndDateUtc = start.AddDays(LicenseConstants.TrialDays)
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<LicenseStatusResponse>>(_json);
        Assert.NotNull(body?.Data);
        Assert.True(body!.Data!.TrialStartDateUtc <= DateTime.UtcNow.AddMinutes(1));
        Assert.True((DateTime.UtcNow - body.Data.TrialStartDateUtc).TotalDays >= 4.5);
        Assert.Equal(LicenseConstants.TrialDays, (body.Data.TrialEndDateUtc - body.Data.TrialStartDateUtc).TotalDays, 0);
    }

    [Fact]
    public async Task Status_RequiresAuth_AndBlocksOtherUser()
    {
        var client = _factory.CreateClient();
        var deviceId = "device-" + Guid.NewGuid().ToString("N");
        await InitializeAsync(client, deviceId);

        var owner = UniqueEmail("lic-owner");
        await RegisterAsync(client, owner, deviceId);
        var ownerLogin = await LoginAsync(client, owner, deviceId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerLogin.AccessToken);

        var validate = await client.PostAsJsonAsync("/api/license/validate", new LicenseValidateRequest { DeviceId = deviceId });
        Assert.Equal(HttpStatusCode.OK, validate.StatusCode);

        var other = UniqueEmail("lic-other");
        var otherDevice = "device-" + Guid.NewGuid().ToString("N");
        await RegisterAsync(client, other, otherDevice);
        var otherLogin = await LoginAsync(client, other, otherDevice);

        var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherLogin.AccessToken);
        var blocked = await otherClient.PostAsJsonAsync("/api/license/validate", new LicenseValidateRequest { DeviceId = deviceId });
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
    }

    [Fact]
    public async Task Initialize_DoesNotReturnSecrets()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/license/initialize", new LicenseInitializeRequest
        {
            DeviceId = "device-" + Guid.NewGuid().ToString("N")
        });
        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PasswordHash", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SigningKey", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RefreshToken", text, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<LicenseStatusResponse> InitializeAsync(HttpClient client, string deviceId)
    {
        var response = await client.PostAsJsonAsync("/api/license/initialize", new LicenseInitializeRequest { DeviceId = deviceId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<LicenseStatusResponse>>(_json);
        Assert.NotNull(body?.Data);
        return body!.Data!;
    }

    private async Task RegisterAsync(HttpClient client, string email, string deviceId)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = "License Tester",
            Email = email,
            MobileNumber = UniqueMobile(),
            Password = "Password1!",
            ConfirmPassword = "Password1!",
            CompanyName = "License Co",
            CompanyEmail = email,
            CompanyMobile = UniqueMobile(),
            Plan = "Starter",
            InstallationId = Guid.NewGuid(),
            DeviceId = deviceId,
            DeviceName = "Tests",
            Platform = "Tests"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<LoginResponse> LoginAsync(HttpClient client, string email, string deviceId)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password1!",
            DeviceId = deviceId,
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
