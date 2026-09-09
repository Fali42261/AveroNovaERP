using AveroNova.Application.DTOs.Auth;
using AveroNova.Application.DTOs.Sync;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using AveroNova.App.UI.Services.Api;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Security;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class BusinessSyncClientTests : IAsyncLifetime
{
    private readonly Guid _companyId = Guid.NewGuid();
    private string _path = null!;
    private IDbContextFactory<LocalAppDbContext> _factory = null!;
    private AppSessionContext _session = null!;

    public async Task InitializeAsync()
    {
        _path = Path.Combine(Path.GetTempPath(), $"averonova-business-sync-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LocalAppDbContext>()
            .UseSqlite($"Data Source={_path}").Options;
        _factory = new Factory(options);
        await using (var db = await _factory.CreateDbContextAsync())
            await db.Database.EnsureCreatedAsync();

        _session = new AppSessionContext();
        _session.SetFromLocal(
            new LocalUserEntity { Id = Guid.NewGuid(), FullName = "Owner", Email = "owner@test.local" },
            new LocalCompanyEntity { Id = _companyId, CompanyName = "Current" },
            ["Company Owner"], ["Sales.Create"], Guid.NewGuid());
    }

    public Task DisposeAsync()
    {
        try { File.Delete(_path); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SyncNow_PushesInvoiceBeforePayment_AndAcknowledgesLocalQueue()
    {
        var invoice = await CreateInvoiceAndPaymentAsync();
        var api = new FakeBusinessSyncApi();
        var sync = CreateSync(api, new FakeTokenStore("access-token"));

        Assert.True(await sync.SyncNowAsync());
        Assert.NotNull(api.LastRequest);
        Assert.Equal(new[] { "Invoice", "Payment" }, api.LastRequest!.Items.Select(i => i.EntityType).ToArray());
        Assert.All(api.LastRequest.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.PayloadJson)));

        await using var db = await _factory.CreateDbContextAsync();
        Assert.All(await db.SyncQueue.ToListAsync(), q => Assert.Equal((int)RecordSyncStatus.Synced, q.Status));
        Assert.Equal((int)RecordSyncStatus.Synced,
            (await db.Invoices.SingleAsync(i => i.Id == invoice.LocalId)).SyncStatus);
        Assert.Equal((int)RecordSyncStatus.Synced, (await db.Payments.SingleAsync()).SyncStatus);
    }

    [Fact]
    public async Task SyncNow_CoalescesMultipleUpdatesForSamePayment()
    {
        await CreateInvoiceAndPaymentAsync();
        var payments = new LocalPaymentService(_factory, _session);
        var payment = (await payments.GetAllAsync(_companyId)).Single();
        payment.Amount = 75m;
        Assert.True((await payments.UpdateAsync(payment)).Ok);

        var api = new FakeBusinessSyncApi();
        Assert.True(await CreateSync(api, new FakeTokenStore("token")).SyncNowAsync());

        Assert.Equal(1, api.LastRequest!.Items.Count(i => i.EntityType == "Payment"));
        Assert.Contains("75", api.LastRequest.Items.Single(i => i.EntityType == "Payment").PayloadJson);
        await using var db = await _factory.CreateDbContextAsync();
        Assert.All(await db.SyncQueue.Where(q => q.EntityType == "Payment").ToListAsync(),
            q => Assert.Equal((int)RecordSyncStatus.Synced, q.Status));
    }

    [Fact]
    public async Task NetworkFailure_KeepsBusinessItemsPendingForRetry()
    {
        await CreateInvoiceAndPaymentAsync();
        var api = new FakeBusinessSyncApi { NetworkFailure = true };

        Assert.False(await CreateSync(api, new FakeTokenStore("token")).SyncNowAsync());
        await using var db = await _factory.CreateDbContextAsync();
        Assert.All(await db.SyncQueue.ToListAsync(), q => Assert.Equal((int)RecordSyncStatus.Pending, q.Status));
        Assert.All(await db.SyncQueue.ToListAsync(), q => Assert.Equal(1, q.RetryCount));
    }

    [Fact]
    public async Task UnexpectedTransportFailure_RecoversSyncingItemsForRetry()
    {
        await CreateInvoiceAndPaymentAsync();
        var api = new FakeBusinessSyncApi { ThrowUnexpectedly = true };

        Assert.False(await CreateSync(api, new FakeTokenStore("token")).SyncNowAsync());

        await using var db = await _factory.CreateDbContextAsync();
        Assert.All(await db.SyncQueue.ToListAsync(),
            q => Assert.Equal((int)RecordSyncStatus.Pending, q.Status));
    }

    private async Task<InvoiceModel> CreateInvoiceAndPaymentAsync()
    {
        var customerId = Guid.NewGuid();
        var invoice = new InvoiceModel
        {
            CompanyId = _companyId,
            CustomerId = customerId,
            CustomerName = "Customer",
            Status = InvoiceStatus.Sent,
            Items = [new InvoiceLineItem { ProductId = Guid.NewGuid(), ProductName = "Item", Quantity = 1, UnitPrice = 100m }]
        };
        var billing = new LocalBillingService(_factory, _session);
        Assert.True((await billing.CreateAsync(invoice)).Ok);

        var payment = new PaymentModel
        {
            CompanyId = _companyId, PartyId = customerId, PartyName = "Customer",
            InvoiceId = invoice.LocalId, Amount = 50m, Status = PaymentStatus.Completed,
            Method = PaymentMethod.Cash, PaymentDate = DateTime.Today
        };
        Assert.True((await new LocalPaymentService(_factory, _session).CreateAsync(payment)).Ok);
        return invoice;
    }

    private RegistrationSyncService CreateSync(IBusinessSyncApiClient api, ISecureTokenStore tokens)
        => new(
            _factory,
            new FakeAuthApi(),
            new FakePendingSecrets(),
            new FakeConnectivity(),
            _session,
            NullLogger<RegistrationSyncService>.Instance,
            businessApi: api,
            tokens: tokens);

    private sealed class FakeBusinessSyncApi : IBusinessSyncApiClient
    {
        public BusinessSyncBatchRequest? LastRequest { get; private set; }
        public bool NetworkFailure { get; init; }
        public bool ThrowUnexpectedly { get; init; }

        public Task<ApiCallResult<BusinessSyncBatchResponse>> PushAsync(
            BusinessSyncBatchRequest request, string accessToken, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (ThrowUnexpectedly)
                throw new HttpRequestException("Unexpected transport failure.");
            if (NetworkFailure)
                return Task.FromResult(ApiCallResult<BusinessSyncBatchResponse>.Fail(0, "offline", network: true));
            var now = DateTime.UtcNow;
            return Task.FromResult(ApiCallResult<BusinessSyncBatchResponse>.Ok(new BusinessSyncBatchResponse
            {
                ServerTimeUtc = now,
                Items = request.Items.Select(i => new BusinessSyncItemResult
                {
                    QueueId = i.QueueId, EntityId = i.EntityId, EntityType = i.EntityType,
                    Success = true, ServerUpdatedAtUtc = now
                }).ToList()
            }, 200));
        }
    }

    private sealed class FakeConnectivity : IConnectivityService
    {
        public ConnectivityStatus Status => ConnectivityStatus.Online;
        public bool IsOnline => true;
        public int PendingCount => 0;
        public event EventHandler<ConnectivityStatus>? StatusChanged;
        public void UpdateStatus(ConnectivityStatus status) { }
        public void IncrementPending() { }
        public void DecrementPending(int count = 1) { }
    }

    private sealed class FakeTokenStore(string? accessToken) : ISecureTokenStore
    {
        public Task SetAccessTokenAsync(string token, DateTime expiresUtc) => Task.CompletedTask;
        public Task SetRefreshTokenAsync(string token) => Task.CompletedTask;
        public Task SetSessionIdAsync(Guid sessionId) => Task.CompletedTask;
        public Task<string?> GetAccessTokenAsync() => Task.FromResult(accessToken);
        public Task<string?> GetRefreshTokenAsync() => Task.FromResult<string?>(null);
        public Task<DateTime?> GetAccessTokenExpiryAsync() => Task.FromResult<DateTime?>(null);
        public Task<Guid?> GetSessionIdAsync() => Task.FromResult<Guid?>(null);
        public Task ClearAsync() => Task.CompletedTask;
    }

    private sealed class FakePendingSecrets : IPendingRegistrationSecretStore
    {
        public Task SetPendingPasswordAsync(Guid registrationUserId, string password) => Task.CompletedTask;
        public Task<string?> GetPendingPasswordAsync(Guid registrationUserId) => Task.FromResult<string?>(null);
        public Task ClearPendingPasswordAsync(Guid registrationUserId) => Task.CompletedTask;
    }

    private sealed class FakeAuthApi : IAuthApiClient
    {
        public Task<ApiCallResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(ApiCallResult<LoginResponse>.Fail(500, "unused"));
        public Task<ApiCallResult<LoginResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(ApiCallResult<LoginResponse>.Fail(500, "unused"));
        public Task<ApiCallResult> LogoutAsync(LogoutRequest request, string accessToken, CancellationToken cancellationToken = default)
            => Task.FromResult(ApiCallResult.Fail(500, "unused"));
        public Task<ApiCallResult<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(ApiCallResult<RegisterResponse>.Fail(500, "unused"));
        public Task<ApiCallResult<MeResponse>> MeAsync(string accessToken, CancellationToken cancellationToken = default)
            => Task.FromResult(ApiCallResult<MeResponse>.Fail(500, "unused"));
    }

    private sealed class Factory(DbContextOptions<LocalAppDbContext> options) : IDbContextFactory<LocalAppDbContext>
    {
        public LocalAppDbContext CreateDbContext() => new(options);
    }
}
