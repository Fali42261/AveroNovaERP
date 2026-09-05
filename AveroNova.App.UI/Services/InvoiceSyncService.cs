using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Api;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Security;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AveroNova.App.UI.Services;

public sealed class InvoiceSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly object SubscriptionGate = new();
    private static bool _connectivitySubscribed;

    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly IApiClient _api;
    private readonly ISecureTokenStore _tokens;
    private readonly IConnectivityService _connectivity;
    private readonly ILogger<InvoiceSyncService> _logger;

    public InvoiceSyncService(IDbContextFactory<LocalAppDbContext> dbFactory, IApiClient api,
        ISecureTokenStore tokens, IConnectivityService connectivity, ILogger<InvoiceSyncService> logger)
    {
        _dbFactory = dbFactory;
        _api = api;
        _tokens = tokens;
        _connectivity = connectivity;
        _logger = logger;

        lock (SubscriptionGate)
        {
            if (!_connectivitySubscribed)
            {
                _connectivity.StatusChanged += OnConnectivityChanged;
                _connectivitySubscribed = true;
            }
        }
    }

    public async Task SyncPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!_connectivity.IsOnline || !await Gate.WaitAsync(0, cancellationToken)) return;
        try
        {
            var token = await _tokens.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) return;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var items = await db.SyncQueue
                .Where(x => x.EntityType == "Invoice" &&
                    (x.Status == (int)RecordSyncStatus.Pending || x.Status == (int)RecordSyncStatus.Failed))
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            foreach (var item in items)
            {
                item.Status = (int)RecordSyncStatus.Syncing;
                item.LastAttemptAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                ApiCallResult result;
                var operation = (SyncOperation)item.Operation;
                if (operation == SyncOperation.Delete)
                {
                    result = await _api.DeleteAsync($"api/invoices/{item.EntityId:D}", token, cancellationToken);
                }
                else
                {
                    InvoiceSyncPayload? payload;
                    try
                    {
                        payload = string.IsNullOrWhiteSpace(item.PayloadJson)
                            ? null
                            : JsonSerializer.Deserialize<InvoiceSyncPayload>(item.PayloadJson, JsonOptions);
                    }
                    catch
                    {
                        payload = null;
                    }

                    if (payload is null || payload.Id == Guid.Empty || payload.CompanyId == Guid.Empty ||
                        payload.CustomerId == Guid.Empty || string.IsNullOrWhiteSpace(payload.InvoiceNumber))
                    {
                        MarkFailed(item, "Invoice sync payload is incomplete.");
                        await db.SaveChangesAsync(cancellationToken);
                        continue;
                    }

                    ApiCallResult<InvoiceSyncResponse> typed = operation switch
                    {
                        SyncOperation.Create => await _api.PostAsync<InvoiceSyncResponse>("api/invoices", payload, token, cancellationToken),
                        SyncOperation.Update => await _api.PutAsync<InvoiceSyncResponse>($"api/invoices/{payload.Id:D}", payload, token, cancellationToken),
                        _ => ApiCallResult<InvoiceSyncResponse>.Fail(400, "Unsupported invoice sync operation.")
                    };
                    result = typed;

                    if (typed.Success && typed.Data is not null)
                    {
                        var row = await db.Invoices.FirstOrDefaultAsync(x => x.Id == payload.Id, cancellationToken);
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
                    item.Error = result.Error ?? "Invoice sync failed.";
                    item.LastAttemptAt = DateTime.UtcNow;
                    item.Status = (int)(result.IsNetworkError || item.RetryCount < 5
                        ? RecordSyncStatus.Pending
                        : RecordSyncStatus.Failed);
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
            _logger.LogWarning(ex, "Invoice sync failed; local data remains queued.");
        }
        finally
        {
            Gate.Release();
        }
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

    private sealed class InvoiceSyncPayload
    {
        public Guid Id { get; set; }
        public Guid CompanyId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public string ItemsJson { get; set; } = "[]";
        public decimal DiscountPct { get; set; }
        public decimal TaxPct { get; set; }
        public int PaymentMethod { get; set; }
        public string Notes { get; set; } = string.Empty;
        public int Status { get; set; }
        public decimal PaidAmount { get; set; }
        public long SyncVersion { get; set; }
    }

    private sealed class InvoiceSyncResponse
    {
        public Guid Id { get; set; }
        public long SyncVersion { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
