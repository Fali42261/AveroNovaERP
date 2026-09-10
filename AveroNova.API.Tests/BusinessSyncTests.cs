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
        var paymentRecord = records.Single(x => x.EntityId == paymentId);
        await db.Entry(paymentRecord).ReloadAsync();
        Assert.True(paymentRecord.IsDeleted);
    }

    [Fact]
    public async Task Push_IsolatesInvalidItem_AndCommitsIndependentRecords()
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("exceeds", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.SyncQueueItems.AnyAsync(x => x.CompanyId == companyId && x.EntityId == invoiceId));
        Assert.False(await db.SyncQueueItems.AnyAsync(x => x.CompanyId == companyId && x.EntityType == "Payment"));
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

    [Fact]
    public async Task Push_StaleExpectedVersion_ReturnsConflictAndRetainsServerCopy()
    {
        var (client, companyId) = await AuthenticatedClientAsync("conflict");
        var invoiceId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var first = await client.PostAsJsonAsync("/api/sync/business/push", new BusinessSyncBatchRequest
        {
            Items = [InvoiceItem(companyId, invoiceId, customerId, 100m)]
        });
        var firstBody = await first.Content.ReadFromJsonAsync<ApiEnvelope<BusinessSyncBatchResponse>>(_json);
        var firstVersion = firstBody!.Data!.Items.Single().ServerVersion;
        Assert.True(firstVersion > 0);

        var currentUpdate = InvoiceItem(companyId, invoiceId, customerId, 120m);
        currentUpdate.ExpectedServerVersion = firstVersion;
        var current = await client.PostAsJsonAsync("/api/sync/business/push", new BusinessSyncBatchRequest
        {
            Items = [currentUpdate]
        });
        var currentBody = await current.Content.ReadFromJsonAsync<ApiEnvelope<BusinessSyncBatchResponse>>(_json);
        var currentVersion = currentBody!.Data!.Items.Single().ServerVersion;
        Assert.True(currentVersion > firstVersion);

        var staleUpdate = InvoiceItem(companyId, invoiceId, customerId, 90m);
        staleUpdate.ExpectedServerVersion = firstVersion;
        var stale = await client.PostAsJsonAsync("/api/sync/business/push", new BusinessSyncBatchRequest
        {
            Items = [staleUpdate]
        });
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        var staleBody = await stale.Content.ReadFromJsonAsync<ApiEnvelope<BusinessSyncBatchResponse>>(_json);
        var conflict = staleBody!.Data!.Items.Single();
        Assert.False(conflict.Success);
        Assert.True(conflict.Conflict);
        Assert.Equal(currentVersion, conflict.ServerVersion);
        Assert.Contains("120", conflict.ServerPayloadJson);
    }

    [Fact]
    public async Task SupplierPayment_ReconcilesPurchasePayable_AndRejectsOverpayment()
    {
        var (client, companyId) = await AuthenticatedClientAsync("payable");
        var purchaseId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var ok = await client.PostAsJsonAsync("/api/sync/business/push", new BusinessSyncBatchRequest
        {
            Items =
            [
                PurchaseItem(companyId, purchaseId, supplierId, 120m),
                PaymentItem(companyId, paymentId, purchaseId, supplierId, 70m, isSupplier: true)
            ]
        });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var purchase = await db.SyncQueueItems.SingleAsync(x => x.CompanyId == companyId && x.EntityType == "Purchase" && x.EntityId == purchaseId);
        Assert.Equal(70m, ReadDecimal(purchase.PayloadJson!, "PaidAmount"));

        var rejected = await client.PostAsJsonAsync("/api/sync/business/push", new BusinessSyncBatchRequest
        {
            Items = [PaymentItem(companyId, Guid.NewGuid(), purchaseId, supplierId, 51m, isSupplier: true)]
        });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains("exceeds", await rejected.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PurchaseReturn_ReconcilesCredit_AndRejectsOverReturnAndPaymentBeyondNetPayable()
    {
        var (client, companyId) = await AuthenticatedClientAsync("purchase-return");
        var purchaseId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var returnId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync("/api/sync/business/push", new BusinessSyncBatchRequest
        {
            Items =
            [
                PurchaseItem(companyId, purchaseId, supplierId, 100m, productId, 5),
                PurchaseReturnItem(companyId, returnId, purchaseId, supplierId, productId, 2, 40m)
            ]
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var purchase = await db.SyncQueueItems.SingleAsync(x => x.CompanyId == companyId && x.EntityType == "Purchase" && x.EntityId == purchaseId);
        Assert.Equal(40m, ReadDecimal(purchase.PayloadJson!, "ReturnCreditAmount"));

        var overReturn = await client.PostAsJsonAsync("/api/sync/business/push", new BusinessSyncBatchRequest
        {
            Items = [PurchaseReturnItem(companyId, Guid.NewGuid(), purchaseId, supplierId, productId, 4, 60m)]
        });
        Assert.Equal(HttpStatusCode.OK, overReturn.StatusCode);
        Assert.Contains("exceeds", await overReturn.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var overPayment = await client.PostAsJsonAsync("/api/sync/business/push", new BusinessSyncBatchRequest
        {
            Items = [PaymentItem(companyId, Guid.NewGuid(), purchaseId, supplierId, 61m, isSupplier: true)]
        });
        Assert.Equal(HttpStatusCode.OK, overPayment.StatusCode);
        Assert.Contains("exceeds", await overPayment.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var deleted = await client.PostAsJsonAsync("/api/sync/business/push", new BusinessSyncBatchRequest
        {
            Items = [new BusinessSyncItemRequest { QueueId=Guid.NewGuid(),EntityType="PurchaseReturn",EntityId=returnId,CompanyId=companyId,Operation=SyncOperation.Delete,ClientUpdatedAtUtc=DateTime.UtcNow }]
        });
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        await db.Entry(purchase).ReloadAsync();
        Assert.Equal(0m, ReadDecimal(purchase.PayloadJson!, "ReturnCreditAmount"));
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
        Guid companyId, Guid paymentId, Guid invoiceId, Guid customerId, decimal amount, bool isSupplier = false)
        => new()
        {
            QueueId = Guid.NewGuid(), EntityType = "Payment", EntityId = paymentId,
            CompanyId = companyId, Operation = SyncOperation.Update, ClientUpdatedAtUtc = DateTime.UtcNow,
            PayloadJson = JsonSerializer.Serialize(new
            {
                Id = paymentId, CompanyId = companyId, PaymentNumber = "PAY-SYNC-1",
                PartyId = customerId, PartyName = isSupplier ? "Supplier" : "Customer", IsSupplier = isSupplier,
                InvoiceId = invoiceId, InvoiceNumber = "INV-SYNC-1", Amount = amount,
                Method = 0, PaymentDate = DateTime.Today, Reference = "", Notes = "",
                Status = 1, UpdatedAtUtc = DateTime.UtcNow
            })
        };

    private static BusinessSyncItemRequest PurchaseItem(Guid companyId, Guid purchaseId, Guid supplierId, decimal total,
        Guid? productId = null, int quantity = 1)
        => new()
        {
            QueueId=Guid.NewGuid(),EntityType="Purchase",EntityId=purchaseId,CompanyId=companyId,
            Operation=SyncOperation.Update,ClientUpdatedAtUtc=DateTime.UtcNow,
            PayloadJson=JsonSerializer.Serialize(new
            {
                Id=purchaseId,CompanyId=companyId,PurchaseNumber="PO-SYNC-1",SupplierId=supplierId,
                SupplierName="Supplier",PurchaseDate=DateTime.Today,DueDate=DateTime.Today.AddDays(30),
                ItemsJson=JsonSerializer.Serialize(new[]{new{ProductId=productId??Guid.NewGuid(),ProductName="Part",Quantity=quantity,UnitPrice=total/quantity,TaxPct=0m}}),PaymentMethod=0,Reference="",Notes="",Status=3,PaidAmount=999m,ReturnCreditAmount=999m,
                GrandTotal=total,UpdatedAtUtc=DateTime.UtcNow
            })
        };

    private static BusinessSyncItemRequest PurchaseReturnItem(Guid companyId, Guid returnId, Guid purchaseId,
        Guid supplierId, Guid productId, int quantity, decimal refund)
        => new()
        {
            QueueId=Guid.NewGuid(),EntityType="PurchaseReturn",EntityId=returnId,CompanyId=companyId,
            Operation=SyncOperation.Update,ClientUpdatedAtUtc=DateTime.UtcNow,
            PayloadJson=JsonSerializer.Serialize(new
            {
                Id=returnId,CompanyId=companyId,ReturnNumber="PR-SYNC-1",PurchaseId=purchaseId,SupplierId=supplierId,
                ReturnDate=DateTime.Today,ItemsJson=JsonSerializer.Serialize(new[]{new{ProductId=productId,ProductName="Part",Quantity=quantity,UnitPrice=20m}}),
                Reason="Defective",Notes="",RefundAmount=refund,Status=3,UpdatedAtUtc=DateTime.UtcNow
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
