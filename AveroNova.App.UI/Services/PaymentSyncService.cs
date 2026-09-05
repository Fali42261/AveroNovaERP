using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Services.Api;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Security;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AveroNova.App.UI.Services;

public interface IPaymentSyncService
{
    Task SyncPendingAsync(CancellationToken cancellationToken = default);
}

public sealed class PaymentSyncService : IPaymentSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly IApiClient _api;
    private readonly ISecureTokenStore _tokens;
    private readonly IConnectivityService _connectivity;
    private readonly ILogger<PaymentSyncService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PaymentSyncService(IDbContextFactory<LocalAppDbContext> dbFactory, IApiClient api,
        ISecureTokenStore tokens, IConnectivityService connectivity, ILogger<PaymentSyncService> logger)
    {
        _dbFactory = dbFactory; _api = api; _tokens = tokens; _connectivity = connectivity; _logger = logger;
        _connectivity.StatusChanged += OnConnectivityChanged;
    }

    public async Task SyncPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!_connectivity.IsOnline || !await _gate.WaitAsync(0, cancellationToken)) return;
        try
        {
            var token = await _tokens.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) return;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var items = await db.SyncQueue
                .Where(x => x.EntityType == "Payment" && (x.Status == (int)RecordSyncStatus.Pending || x.Status == (int)RecordSyncStatus.Failed))
                .OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);

            foreach (var item in items)
            {
                item.Status = (int)RecordSyncStatus.Syncing;
                item.LastAttemptAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                ApiCallResult result;
                var op = (SyncOperation)item.Operation;
                if (op == SyncOperation.Delete)
                {
                    result = await _api.DeleteAsync($"api/payments/{item.EntityId:D}", token, cancellationToken);
                }
                else
                {
                    PaymentSyncPayload? payload;
                    try { payload = string.IsNullOrWhiteSpace(item.PayloadJson) ? null : JsonSerializer.Deserialize<PaymentSyncPayload>(item.PayloadJson, JsonOptions); }
                    catch { payload = null; }

                    if (payload is null || payload.Id == Guid.Empty || payload.CompanyId == Guid.Empty || payload.Amount <= 0 || string.IsNullOrWhiteSpace(payload.PaymentNumber))
                    {
                        MarkFailed(item, "Payment sync payload is incomplete.");
                        await db.SaveChangesAsync(cancellationToken);
                        continue;
                    }

                    ApiCallResult<PaymentSyncResponse> typed = op switch
                    {
                        SyncOperation.Create => await _api.PostAsync<PaymentSyncResponse>("api/payments", payload, token, cancellationToken),
                        SyncOperation.Update => await _api.PutAsync<PaymentSyncResponse>($"api/payments/{payload.Id:D}", payload, token, cancellationToken),
                        _ => ApiCallResult<PaymentSyncResponse>.Fail(400, "Unsupported payment sync operation.")
                    };
                    result = typed;

                    if (typed.Success && typed.Data is not null)
                    {
                        var row = await db.Payments.FirstOrDefaultAsync(x => x.Id == payload.Id, cancellationToken);
                        if (row is not null)
                        {
                            row.ServerId = typed.Data.Id;
                            row.SyncStatus = (int)RecordSyncStatus.Synced;
                            row.LastSyncedAtUtc = DateTime.UtcNow;
                            row.SyncError = null;
                        }
                    }
                }

                if (!result.Success)
                {
                    item.RetryCount++;
                    item.Error = result.Error ?? "Payment sync failed.";
                    item.LastAttemptAt = DateTime.UtcNow;
                    item.Status = (int)(result.IsNetworkError || item.RetryCount < 5 ? RecordSyncStatus.Pending : RecordSyncStatus.Failed);
                    await db.SaveChangesAsync(cancellationToken);
                    continue;
                }

                item.Status = (int)RecordSyncStatus.Synced;
                item.Error = null;
                item.SyncedAt = DateTime.UtcNow;
                item.LastAttemptAt = DateTime.UtcNow;
                _connectivity.DecrementPending();
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Payment sync failed; local data remains queued.");
        }
        finally { _gate.Release(); }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityStatus status)
    {
        if (status is ConnectivityStatus.Online or ConnectivityStatus.Synced or ConnectivityStatus.PendingSync)
            _ = SyncPendingAsync();
    }

    private static void MarkFailed(LocalSyncQueueEntity item, string error)
    {
        item.RetryCount++;
        item.Error = error;
        item.LastAttemptAt = DateTime.UtcNow;
        item.Status = (int)RecordSyncStatus.Failed;
    }

    private sealed class PaymentSyncPayload
    {
        public Guid Id { get; set; } public Guid CompanyId { get; set; } public string PaymentNumber { get; set; } = string.Empty;
        public Guid PartyId { get; set; } public string PartyName { get; set; } = string.Empty; public bool IsSupplier { get; set; }
        public Guid? InvoiceId { get; set; } public string InvoiceNumber { get; set; } = string.Empty; public decimal Amount { get; set; }
        public int Method { get; set; } public DateTime PaymentDate { get; set; } public string Reference { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty; public int Status { get; set; } public long SyncVersion { get; set; }
    }

    private sealed class PaymentSyncResponse { public Guid Id { get; set; } public long SyncVersion { get; set; } public DateTime? UpdatedAt { get; set; } }
}
