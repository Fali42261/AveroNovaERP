using System.Text.Json;
using AveroNova.Application.DTOs.Auth;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Api;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Security;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AveroNova.App.UI.Services;

/// <summary>
/// Offline-first sync coordinator. Local writes never wait for the server; supported
/// queues are pushed automatically when connectivity is available.
/// </summary>
public sealed class RegistrationSyncService : ISyncService
{
    private static readonly string[] RegistrationEntityTypes = ["User", "Company", "UserCompany", "Subscription"];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly IAuthApiClient _authApi;
    private readonly IApiClient _api;
    private readonly ISecureTokenStore _tokens;
    private readonly IPendingRegistrationSecretStore _pendingSecrets;
    private readonly IConnectivityService _connectivity;
    private readonly IAppSessionContext _session;
    private readonly ILogger<RegistrationSyncService> _logger;
    private readonly ILicenseService? _licenses;
    private readonly object _gate = new();
    private bool _isSyncing;

    public RegistrationSyncService(IDbContextFactory<LocalAppDbContext> dbFactory, IAuthApiClient authApi,
        IApiClient api, ISecureTokenStore tokens, IPendingRegistrationSecretStore pendingSecrets,
        IConnectivityService connectivity, IAppSessionContext session, ILogger<RegistrationSyncService> logger,
        ILicenseService? licenses = null)
    {
        _dbFactory = dbFactory; _authApi = authApi; _api = api; _tokens = tokens; _pendingSecrets = pendingSecrets;
        _connectivity = connectivity; _session = session; _logger = logger; _licenses = licenses;
        _connectivity.StatusChanged += OnConnectivityChanged;
    }

    public bool IsSyncing => _isSyncing;
    public DateTime? LastSyncAt { get; private set; }
    public int PendingCount { get; private set; }
    public int FailedCount { get; private set; }
    public event EventHandler<SyncHistoryModel>? SyncCompleted;

    public async Task<bool> SyncNowAsync()
    {
        lock (_gate) { if (_isSyncing) return false; _isSyncing = true; }
        try
        {
            await RefreshCountsAsync();
            if (!_connectivity.IsOnline)
            {
                RaiseHistory(false, 0, "Offline — changes are saved locally and will sync automatically.");
                return false;
            }

            await using var db = await _dbFactory.CreateDbContextAsync();
            var pending = await db.SyncQueue
                .Where(q => q.Status == (int)RecordSyncStatus.Pending || q.Status == (int)RecordSyncStatus.Failed)
                .OrderBy(q => q.CreatedAt).ToListAsync();
            if (pending.Count == 0)
            {
                RaiseHistory(true, 0, "All changes are synced."); LastSyncAt = DateTime.UtcNow; return true;
            }

            var succeeded = 0; var failed = 0;
            var registrationItems = pending.Where(p => RegistrationEntityTypes.Contains(p.EntityType, StringComparer.OrdinalIgnoreCase)
                                                        && LooksLikeRegistrationPayload(p.PayloadJson)).ToList();
            if (registrationItems.Count > 0)
            {
                var ok = await SyncRegistrationBatchAsync(db, registrationItems);
                if (ok) succeeded += registrationItems.Count; else failed += registrationItems.Count;
            }

            var registrationIds = registrationItems.Select(x => x.Id).ToHashSet();
            foreach (var item in pending.Where(p => !registrationIds.Contains(p.Id) && p.EntityType.Equals("Company", StringComparison.OrdinalIgnoreCase)))
            { if (await SyncCompanyAsync(db, item)) succeeded++; else failed++; }

            foreach (var item in pending.Where(p => p.EntityType.Equals("Customer", StringComparison.OrdinalIgnoreCase)))
            { if (await SyncCustomerAsync(db, item)) succeeded++; else failed++; }

            foreach (var item in pending.Where(p => p.EntityType.Equals("Product", StringComparison.OrdinalIgnoreCase)))
            { if (await SyncProductAsync(db, item)) succeeded++; else failed++; }

            foreach (var item in pending.Where(p => p.EntityType.Equals("StockMovement", StringComparison.OrdinalIgnoreCase)))
            { if (await SyncStockMovementAsync(db, item)) succeeded++; else failed++; }

            if (_licenses is not null)
            {
                try { await _licenses.SyncOnlineIfPossibleAsync(); }
                catch (Exception ex) { _logger.LogWarning(ex, "License sync during SyncNow failed."); }
            }

            await db.SaveChangesAsync();
            await RefreshCountsAsync();
            LastSyncAt = DateTime.UtcNow;
            RaiseHistory(failed == 0, succeeded, failed == 0 ? $"Synced {succeeded} change(s)." : $"Synced {succeeded}; {failed} change(s) remain pending/failed.");
            return failed == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SyncNow failed.");
            RaiseHistory(false, 0, "Unable to synchronize now. Local work is safe and will retry later.");
            return false;
        }
        finally { lock (_gate) _isSyncing = false; }
    }

    public Task<bool> RetryFailedAsync() => SyncNowAsync();

    public async Task<List<SyncHistoryModel>> GetHistoryAsync()
    {
        await RefreshCountsAsync();
        return [new SyncHistoryModel { SyncedAt = LastSyncAt ?? DateTime.UtcNow, Success = FailedCount == 0, ItemsSynced = 0,
            Module = "Sync", Message = PendingCount > 0 ? $"{PendingCount} pending, {FailedCount} failed." : "Queue is clear." }];
    }

    private async Task<bool> SyncStockMovementAsync(LocalAppDbContext db, LocalSyncQueueEntity item)
    {
        var token = await _tokens.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) { KeepPending(item, "Sign in is required before inventory changes can sync."); return false; }

        StockMovementSyncPayload? payload;
        try { payload = string.IsNullOrWhiteSpace(item.PayloadJson) ? null : JsonSerializer.Deserialize<StockMovementSyncPayload>(item.PayloadJson, JsonOptions); }
        catch { MarkFailed([item], "Invalid stock movement sync payload."); return false; }
        if (payload is null || payload.Id == Guid.Empty || payload.CompanyId == Guid.Empty || payload.ProductId == Guid.Empty)
        { MarkFailed([item], "Stock movement sync payload is incomplete."); return false; }

        item.Status = (int)RecordSyncStatus.Syncing;
        item.LastAttemptAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var result = await _api.PostAsync<StockMovementSyncResponse>("api/inventory/movements", payload, token);
        if (!result.Success || result.Data is null)
        {
            item.RetryCount++; item.LastAttemptAt = DateTime.UtcNow; item.Error = result.Error ?? "Inventory sync failed.";
            item.Status = (int)(result.IsNetworkError || item.RetryCount < 5 ? RecordSyncStatus.Pending : RecordSyncStatus.Failed);
            return false;
        }

        var row = await db.StockMovements.FirstOrDefaultAsync(x => x.Id == payload.Id);
        if (row is not null)
        {
            row.ServerId = result.Data.Id;
            row.SyncStatus = (int)RecordSyncStatus.Synced;
            row.LastSyncedAtUtc = DateTime.UtcNow;
            row.SyncError = null;
        }

        MarkSynced(item);
        _connectivity.DecrementPending();
        return true;
    }

    private async Task<bool> SyncProductAsync(LocalAppDbContext db, LocalSyncQueueEntity item)
    {
        var token = await _tokens.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) { KeepPending(item, "Sign in is required before product changes can sync."); return false; }

        item.Status = (int)RecordSyncStatus.Syncing; item.LastAttemptAt = DateTime.UtcNow; await db.SaveChangesAsync();
        var op = (SyncOperation)item.Operation;
        ApiCallResult result;

        if (op == SyncOperation.Delete)
        {
            result = await _api.DeleteAsync($"api/products/{item.EntityId:D}", token);
        }
        else
        {
            ProductSyncPayload? payload;
            try { payload = string.IsNullOrWhiteSpace(item.PayloadJson) ? null : JsonSerializer.Deserialize<ProductSyncPayload>(item.PayloadJson, JsonOptions); }
            catch { MarkFailed([item], "Invalid product sync payload."); return false; }
            if (payload is null || payload.Id == Guid.Empty || payload.CompanyId == Guid.Empty || string.IsNullOrWhiteSpace(payload.Name))
            { MarkFailed([item], "Product sync payload is incomplete."); return false; }

            ApiCallResult<ProductSyncResponse> typed = op switch
            {
                SyncOperation.Create => await _api.PostAsync<ProductSyncResponse>("api/products", payload, token),
                SyncOperation.Update => await _api.PutAsync<ProductSyncResponse>($"api/products/{payload.Id:D}", payload, token),
                _ => ApiCallResult<ProductSyncResponse>.Fail(400, "Unsupported product sync operation.")
            };
            result = typed;
            if (typed.Success && typed.Data is not null)
            {
                var row = await db.Products.FirstOrDefaultAsync(x => x.Id == payload.Id);
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
            item.RetryCount++; item.LastAttemptAt = DateTime.UtcNow; item.Error = result.Error ?? "Product sync failed.";
            item.Status = (int)(result.IsNetworkError || item.RetryCount < 5 ? RecordSyncStatus.Pending : RecordSyncStatus.Failed);
            return false;
        }

        MarkSynced(item); _connectivity.DecrementPending(); return true;
    }

    private async Task<bool> SyncCustomerAsync(LocalAppDbContext db, LocalSyncQueueEntity item)
    {
        var token = await _tokens.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) { KeepPending(item, "Sign in is required before customer changes can sync."); return false; }

        item.Status = (int)RecordSyncStatus.Syncing; item.LastAttemptAt = DateTime.UtcNow; await db.SaveChangesAsync();
        var op = (SyncOperation)item.Operation;
        ApiCallResult result;

        if (op == SyncOperation.Delete)
        {
            result = await _api.DeleteAsync($"api/customers/{item.EntityId:D}", token);
        }
        else
        {
            CustomerSyncPayload? payload;
            try { payload = string.IsNullOrWhiteSpace(item.PayloadJson) ? null : JsonSerializer.Deserialize<CustomerSyncPayload>(item.PayloadJson, JsonOptions); }
            catch { MarkFailed([item], "Invalid customer sync payload."); return false; }
            if (payload is null || payload.Id == Guid.Empty || payload.CompanyId == Guid.Empty || string.IsNullOrWhiteSpace(payload.Name))
            { MarkFailed([item], "Customer sync payload is incomplete."); return false; }

            ApiCallResult<CustomerSyncResponse> typed = op switch
            {
                SyncOperation.Create => await _api.PostAsync<CustomerSyncResponse>("api/customers", payload, token),
                SyncOperation.Update => await _api.PutAsync<CustomerSyncResponse>($"api/customers/{payload.Id:D}", payload, token),
                _ => ApiCallResult<CustomerSyncResponse>.Fail(400, "Unsupported customer sync operation.")
            };
            result = typed;
            if (typed.Success && typed.Data is not null)
            {
                var row = await db.Customers.FirstOrDefaultAsync(x => x.Id == payload.Id);
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
            item.RetryCount++; item.LastAttemptAt = DateTime.UtcNow; item.Error = result.Error ?? "Customer sync failed.";
            item.Status = (int)(result.IsNetworkError || item.RetryCount < 5 ? RecordSyncStatus.Pending : RecordSyncStatus.Failed);
            return false;
        }

        MarkSynced(item); _connectivity.DecrementPending(); return true;
    }

    private async Task<bool> SyncCompanyAsync(LocalAppDbContext db, LocalSyncQueueEntity item)
    {
        var accessToken = await _tokens.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken)) { KeepPending(item, "Sign in is required before company changes can sync."); return false; }
        CompanySyncPayload? payload;
        try { payload = string.IsNullOrWhiteSpace(item.PayloadJson) ? null : JsonSerializer.Deserialize<CompanySyncPayload>(item.PayloadJson, JsonOptions); }
        catch { MarkFailed([item], "Invalid company sync payload."); return false; }
        if (payload is null || payload.Id == Guid.Empty || string.IsNullOrWhiteSpace(payload.CompanyName)) { MarkFailed([item], "Company sync payload is incomplete."); return false; }
        item.Status = (int)RecordSyncStatus.Syncing; item.LastAttemptAt = DateTime.UtcNow; await db.SaveChangesAsync();
        var operation = (SyncOperation)item.Operation;
        var result = operation switch
        {
            SyncOperation.Create => await _api.PostAsync<CompanySyncResponse>("api/companies", payload, accessToken),
            SyncOperation.Update => await _api.PutAsync<CompanySyncResponse>($"api/companies/{payload.Id:D}", payload, accessToken),
            _ => ApiCallResult<CompanySyncResponse>.Fail(400, "Unsupported company sync operation.")
        };
        if (!result.Success || result.Data is null)
        {
            item.RetryCount++; item.LastAttemptAt = DateTime.UtcNow; item.Error = result.Error ?? "Company sync failed.";
            item.Status = (int)(result.IsNetworkError || item.RetryCount < 5 ? RecordSyncStatus.Pending : RecordSyncStatus.Failed); return false;
        }
        var row = await db.Companies.FirstOrDefaultAsync(x => x.Id == payload.Id);
        if (row is not null) row.SyncVersion = Math.Max(row.SyncVersion, result.Data.SyncVersion);
        MarkSynced(item); _connectivity.DecrementPending(); return true;
    }

    private async Task<bool> SyncRegistrationBatchAsync(LocalAppDbContext db, List<LocalSyncQueueEntity> items)
    {
        var now = DateTime.UtcNow; foreach (var item in items) { item.Status = (int)RecordSyncStatus.Syncing; item.LastAttemptAt = now; } await db.SaveChangesAsync();
        var payloadJson = items.Select(i => i.PayloadJson).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
        if (string.IsNullOrWhiteSpace(payloadJson)) { MarkFailed(items, "Registration payload missing."); return false; }
        OfflineRegistrationPayload? meta;
        try { meta = JsonSerializer.Deserialize<OfflineRegistrationPayload>(payloadJson, JsonOptions); } catch { MarkFailed(items, "Invalid registration payload."); return false; }
        if (meta is null || meta.ClientUserId == Guid.Empty) { MarkFailed(items, "Invalid registration identity."); return false; }
        var password = await _pendingSecrets.GetPendingPasswordAsync(meta.ClientUserId);
        if (string.IsNullOrWhiteSpace(password)) { MarkFailed(items, "Pending registration password is missing from Secure Storage."); return false; }
        var request = new RegisterRequest { FullName = meta.FullName, Email = meta.Email, MobileNumber = meta.MobileNumber, Password = password,
            ConfirmPassword = password, CompanyName = meta.CompanyName, OwnerName = meta.OwnerName, CompanyEmail = meta.CompanyEmail,
            CompanyMobile = meta.CompanyMobile, Plan = meta.Plan, InstallationId = meta.InstallationId, DeviceId = meta.DeviceId,
            DeviceName = meta.DeviceName, Platform = meta.Platform, ClientUserId = meta.ClientUserId, ClientCompanyId = meta.ClientCompanyId,
            ClientUserCompanyId = meta.ClientUserCompanyId, ClientSubscriptionId = meta.ClientSubscriptionId };
        var result = await _authApi.RegisterAsync(request);
        if (!result.Success || result.Data is null)
        {
            foreach (var item in items) { item.RetryCount++; item.LastAttemptAt = DateTime.UtcNow; item.Error = result.Error ?? "Registration sync failed.";
                item.Status = (int)(result.IsNetworkError || item.RetryCount < 5 ? RecordSyncStatus.Pending : RecordSyncStatus.Failed); }
            return false;
        }
        if (result.Data.UserId != meta.ClientUserId || result.Data.CompanyId != meta.ClientCompanyId || result.Data.SubscriptionId != meta.ClientSubscriptionId)
        { MarkFailed(items, "Server registration identity does not match local identity."); return false; }
        foreach (var item in items) { MarkSynced(item); _connectivity.DecrementPending(); }
        await _pendingSecrets.ClearPendingPasswordAsync(meta.ClientUserId); _ = _session; return true;
    }

    private static bool LooksLikeRegistrationPayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try { var payload = JsonSerializer.Deserialize<OfflineRegistrationPayload>(json, JsonOptions); return payload?.ClientUserId != Guid.Empty && payload?.ClientCompanyId != Guid.Empty; }
        catch { return false; }
    }

    private static void KeepPending(LocalSyncQueueEntity item, string error) { item.Error = error; item.Status = (int)RecordSyncStatus.Pending; item.LastAttemptAt = DateTime.UtcNow; }
    private static void MarkSynced(LocalSyncQueueEntity item) { var now = DateTime.UtcNow; item.Status = (int)RecordSyncStatus.Synced; item.Error = null; item.SyncedAt = now; item.LastAttemptAt = now; }
    private static void MarkFailed(IEnumerable<LocalSyncQueueEntity> items, string error) { var now = DateTime.UtcNow; foreach (var item in items) { item.RetryCount++; item.LastAttemptAt = now; item.Error = error; item.Status = (int)RecordSyncStatus.Failed; } }
    private async Task RefreshCountsAsync() { await using var db = await _dbFactory.CreateDbContextAsync(); PendingCount = await db.SyncQueue.CountAsync(q => q.Status == (int)RecordSyncStatus.Pending); FailedCount = await db.SyncQueue.CountAsync(q => q.Status == (int)RecordSyncStatus.Failed); }
    private void OnConnectivityChanged(object? sender, ConnectivityStatus status) { if (status is ConnectivityStatus.Online or ConnectivityStatus.Synced or ConnectivityStatus.PendingSync) _ = SyncNowAsync(); }
    private void RaiseHistory(bool success, int items, string message) => SyncCompleted?.Invoke(this, new SyncHistoryModel { SyncedAt = DateTime.UtcNow, Success = success, ItemsSynced = items, Module = "Sync", Message = message });

    private sealed class CompanySyncPayload { public Guid Id { get; set; } public string CompanyName { get; set; } = string.Empty; public string? Email { get; set; } public string? MobileNumber { get; set; } public long SyncVersion { get; set; } }
    private sealed class CompanySyncResponse { public Guid Id { get; set; } public long SyncVersion { get; set; } public DateTime? UpdatedAt { get; set; } }
    private sealed class CustomerSyncPayload { public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Name { get; set; } = string.Empty; public string? Email { get; set; } public string? Phone { get; set; } public string? Address { get; set; } public string? City { get; set; } public string? Country { get; set; } public string? TaxNumber { get; set; } public string? Notes { get; set; } public int Status { get; set; } public decimal OutstandingBalance { get; set; } public decimal TotalPurchases { get; set; } public long SyncVersion { get; set; } }
    private sealed class CustomerSyncResponse { public Guid Id { get; set; } public long SyncVersion { get; set; } public DateTime? UpdatedAt { get; set; } }
    private sealed class ProductSyncPayload { public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Name { get; set; } = string.Empty; public string? Sku { get; set; } public string? Barcode { get; set; } public string? Category { get; set; } public string? Brand { get; set; } public string? Unit { get; set; } public decimal PurchasePrice { get; set; } public decimal SellingPrice { get; set; } public decimal TaxPercent { get; set; } public int Stock { get; set; } public int MinimumStock { get; set; } public string? Description { get; set; } public int Status { get; set; } public long SyncVersion { get; set; } }
    private sealed class ProductSyncResponse { public Guid Id { get; set; } public long SyncVersion { get; set; } public DateTime? UpdatedAt { get; set; } }
    private sealed class StockMovementSyncPayload { public Guid Id { get; set; } public Guid CompanyId { get; set; } public Guid ProductId { get; set; } public string? ProductName { get; set; } public string? Sku { get; set; } public int Type { get; set; } public int Quantity { get; set; } public int StockBefore { get; set; } public int StockAfter { get; set; } public string? Reference { get; set; } public string? Notes { get; set; } public string? CreatedBy { get; set; } public long SyncVersion { get; set; } }
    private sealed class StockMovementSyncResponse { public Guid Id { get; set; } public long SyncVersion { get; set; } public DateTime? UpdatedAt { get; set; } }
}
