using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AveroNova.Application.DTOs.Auth;
using AveroNova.Application.DTOs.Sync;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AveroNova.API.Tests;

public sealed class BusinessSyncTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly AuthWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public BusinessSyncTests(AuthWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Push_RequiresAuthentication()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/sync/business/push", new BusinessSyncBatchRequest
        {
            Items = [InvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m)]
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvoiceAndPaymentPush_IsIdempotent_AndReconcilesServerSnapshot()
    {
        var (client, companyId) = await AuthenticatedClientAsync("sync");
        var invoiceId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var batch = new BusinessSyncBatchRequest
        {
            Items =
            [
                InvoiceItem(companyId, invoiceId, customerId, 100m),
                PaymentItem(companyId, paymentId, invoiceId, customerId, 40m)
            ]
        };

        var first = await client.PostAsJsonAsync("/api/sync/business/push", batch);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await client.PostAsJsonAsync("/api/sync/business/push", batch);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var records = await db.SyncQueueItems
            .Where(x => x.CompanyId == companyId && (x.EntityType == "Invoice" || x.EntityType == "Payment"))
            .ToListAsync();
        Assert.Equal(2, records.Count);
        var invoice = records.Single(x => x.EntityType == "Invoice");
        Assert.Equal(40m, ReadDecimal(invoice.PayloadJson!, "PaidAmount"));
        Assert.Equal(2, ReadInt(invoice.PayloadJson!, "Status"));

        var delete = new BusinessSyncBatchRequest
        {
            Items =
            [
                new BusinessSyncItemRequest
                {
                    QueueId = Guid.NewGuid(), EntityType = "Payment", EntityId = paymentId,
                    CompanyId = companyId, Operation = SyncOperation.Delete,
                    ClientUpdatedAtUtc = DateTime.UtcNow
                }
            ]
        };
        var deleted = await client.PostAsJsonAsync("/api/sync/business/push", delete);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        await db.Entry(invoice).ReloadAsync();
        Assert.Equal(0m, ReadDecimal(invoice.PayloadJson!, "PaidAmount"));
        Assert.Equal(1, ReadInt(invoice.PayloadJson!, "Status"));
        Assert.True((await db.SyncQueueItems.SingleAsync(x => x.EntityId == paymentId)).IsDeleted);
    }

    [Fact]
    public async Task Push_RejectsOverpayment_AndRollsBackBatch()
    {
        var (client, companyId) = await AuthenticatedClientAsync("overpay");
        var invoiceId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync("/api/sync/business/push", new BusinessSyncBatchRequest
        {
            Items =
            [
                InvoiceItem(companyId, invoiceId, customerId, 100m),
                PaymentItem(companyId, Guid.NewGuid(), invoiceId, customerId, 101m)
            ]
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("exceeds", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.SyncQueueItems.AnyAsync(x => x.CompanyId == companyId && x.EntityId == invoiceId));
    }

    [Fact]
    public async Task Push_RejectsAnotherCompanyFromAuthenticatedToken()
    {
        var (client, _) = await AuthenticatedClientAsync("isolation");
        var response = await client.PostAsJsonAsync("/api/sync/business/push", new BusinessSyncBatchRequest
        {
            Items = [InvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m)]
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<(HttpClient Client, Guid CompanyId)> AuthenticatedClientAsync(string prefix)
    {
        var client = _factory.CreateClient();
        var email = $"{prefix}.{Guid.NewGuid():N}@example.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = "Sync Tester", Email = email, MobileNumber = UniqueMobile(),
            Password = "Password1!", ConfirmPassword = "Password1!",
            CompanyName = "Sync Test Co", CompanyEmail = email, CompanyMobile = UniqueMobile(),
            Plan = "Starter", InstallationId = Guid.NewGuid(), DeviceId = Guid.NewGuid().ToString("N"),
            DeviceName = "Tests", Platform = "Tests"
        });
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var registered = (await registration.Content.ReadFromJsonAsync<ApiEnvelope<RegisterResponse>>(_json))!.Data!;

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email, Password = "Password1!", DeviceId = "sync-device",
            DeviceName = "Tests", Platform = "Tests"
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = (await loginResponse.Content.ReadFromJsonAsync<ApiEnvelope<LoginResponse>>(_json))!.Data!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return (client, registered.CompanyId);
    }

    private static BusinessSyncItemRequest InvoiceItem(Guid companyId, Guid invoiceId, Guid customerId, decimal total)
        => new()
        {
            QueueId = Guid.NewGuid(), EntityType = "Invoice", EntityId = invoiceId,
            CompanyId = companyId, Operation = SyncOperation.Update, ClientUpdatedAtUtc = DateTime.UtcNow,
            PayloadJson = JsonSerializer.Serialize(new
            {
                Id = invoiceId, CompanyId = companyId, InvoiceNumber = "INV-SYNC-1",
                CustomerId = customerId, CustomerName = "Customer", InvoiceDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30), ItemsJson = "[]", DiscountPct = 0m,
                TaxPct = 0m, PaymentMethod = 0, Notes = "", Status = 1, PaidAmount = 0m,
                GrandTotal = total, UpdatedAtUtc = DateTime.UtcNow
            })
        };

    private static BusinessSyncItemRequest PaymentItem(
        Guid companyId, Guid paymentId, Guid invoiceId, Guid customerId, decimal amount)
        => new()
        {
            QueueId = Guid.NewGuid(), EntityType = "Payment", EntityId = paymentId,
            CompanyId = companyId, Operation = SyncOperation.Update, ClientUpdatedAtUtc = DateTime.UtcNow,
            PayloadJson = JsonSerializer.Serialize(new
            {
                Id = paymentId, CompanyId = companyId, PaymentNumber = "PAY-SYNC-1",
                PartyId = customerId, PartyName = "Customer", IsSupplier = false,
                InvoiceId = invoiceId, InvoiceNumber = "INV-SYNC-1", Amount = amount,
                Method = 0, PaymentDate = DateTime.Today, Reference = "", Notes = "",
                Status = 1, UpdatedAtUtc = DateTime.UtcNow
            })
        };

    private static decimal ReadDecimal(string json, string property)
        => JsonDocument.Parse(json).RootElement.GetProperty(property).GetDecimal();
    private static int ReadInt(string json, string property)
        => JsonDocument.Parse(json).RootElement.GetProperty(property).GetInt32();
    private static string UniqueMobile() => "9" + Guid.NewGuid().ToString("N")[..9];

    private sealed class ApiEnvelope<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
    }
}
