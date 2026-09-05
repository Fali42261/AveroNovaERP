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

public interface IProcurementSyncService
{
    Task SyncPendingAsync(CancellationToken cancellationToken = default);
}

public sealed class ProcurementSyncService : IProcurementSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly IApiClient _api;
    private readonly ISecureTokenStore _tokens;
    private readonly IConnectivityService _connectivity;
    private readonly ILogger<ProcurementSyncService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ProcurementSyncService(IDbContextFactory<LocalAppDbContext> dbFactory, IApiClient api,
        ISecureTokenStore tokens, IConnectivityService connectivity, ILogger<ProcurementSyncService> logger)
    {
        _dbFactory = dbFactory;
        _api = api;
        _tokens = tokens;
        _connectivity = connectivity;
        _logger = logger;
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
                .Where(x => (x.EntityType == "Supplier" || x.EntityType == "Purchase") &&
                            (x.Status == (int)RecordSyncStatus.Pending || x.Status == (int)RecordSyncStatus.Failed))
                .OrderBy(x => x.EntityType == "Supplier" ? 0 : 1)
                .ThenBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            foreach (var item in items)
            {
                item.Status = (int)RecordSyncStatus.Syncing;
                item.LastAttemptAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                var op = (SyncOperation)item.Operation;
                ApiCallResult result;

                if (op == SyncOperation.Delete)
                {
                    var route = item.EntityType == "Supplier" ? "api/suppliers" : "api/purchases";
                    result = await _api.DeleteAsync($"{route}/{item.EntityId:D}", token, cancellationToken);
                }
                else if (item.EntityType == "Supplier")
                {
                    var payload = Deserialize<SupplierSyncPayload>(item.PayloadJson);
                    if (payload is null)
                    {
                        MarkFailed(item, "Supplier sync payload is incomplete.");
                        await db.SaveChangesAsync(cancellationToken);
                        continue;
                    }
                    var typed = op == SyncOperation.Create
                        ? await _api.PostAsync<SyncResponse>("api/suppliers", payload, token, cancellationToken)
                        : await _api.PutAsync<SyncResponse>($"api/suppliers/{payload.Id:D}", payload, token, cancellationToken);
                    result = typed;
                    if (typed.Success && typed.Data is not null)
                    {
                        var row = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == payload.Id, cancellationToken);
                        if (row is not null) { row.ServerId = typed.Data.Id; row.SyncStatus = (int)RecordSyncStatus.Synced; row.LastSyncedAtUtc = DateTime.UtcNow; row.SyncError = null; }
                    }
                }
                else
                {
                    var payload = Deserialize<PurchaseSyncPayload>(item.PayloadJson);
                    if (payload is null)
                    {
                        MarkFailed(item, "Purchase sync payload is incomplete.");
                        await db.SaveChangesAsync(cancellationToken);
                        continue;
                    }
                    var typed = op == SyncOperation.Create
                        ? await _api.PostAsync<SyncResponse>("api/purchases", payload, token, cancellationToken)
                        : await _api.PutAsync<SyncResponse>($"api/purchases/{payload.Id:D}", payload, token, cancellationToken);
                    result = typed;
                    if (typed.Success && typed.Data is not null)
                    {
                        var row = await db.Purchases.FirstOrDefaultAsync(x => x.Id == payload.Id, cancellationToken);
                        if (row is not null) { row.ServerId = typed.Data.Id; row.SyncStatus = (int)RecordSyncStatus.Synced; row.LastSyncedAtUtc = DateTime.UtcNow; row.SyncError = null; }
                    }
                }

                if (!result.Success)
                {
                    item.RetryCount++;
                    item.Error = result.Error ?? "Procurement sync failed.";
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
            _logger.LogWarning(ex, "Procurement sync failed; local data remains queued.");
        }
        finally { _gate.Release(); }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityStatus status)
    {
        if (status is ConnectivityStatus.Online or ConnectivityStatus.Synced or ConnectivityStatus.PendingSync)
            _ = SyncPendingAsync();
    }

    private static T? Deserialize<T>(string json)
    {
        try { return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch { return default; }
    }

    private static void MarkFailed(LocalSyncQueueEntity item, string error)
    {
        item.RetryCount++;
        item.Error = error;
        item.LastAttemptAt = DateTime.UtcNow;
        item.Status = (int)RecordSyncStatus.Failed;
    }

    private sealed class SupplierSyncPayload
    {
        public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty; public string Phone { get; set; } = string.Empty; public string Address { get; set; } = string.Empty;
        public string TaxNumber { get; set; } = string.Empty; public bool IsActive { get; set; }
    }

    private sealed class PurchaseSyncPayload
    {
        public Guid Id { get; set; } public Guid CompanyId { get; set; } public string PurchaseNumber { get; set; } = string.Empty;
        public Guid SupplierId { get; set; } public string SupplierName { get; set; } = string.Empty; public DateTime PurchaseDate { get; set; }
        public DateTime DueDate { get; set; } public string ItemsJson { get; set; } = "[]"; public int PaymentMethod { get; set; }
        public string Reference { get; set; } = string.Empty; public string Notes { get; set; } = string.Empty; public int Status { get; set; }
        public decimal PaidAmount { get; set; }
    }

    private sealed class SyncResponse { public Guid Id { get; set; } public long SyncVersion { get; set; } public DateTime? UpdatedAt { get; set; } }
}
