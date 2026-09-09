using System.Text.Json;
using AveroNova.Application.DTOs.Auth;
using AveroNova.Application.DTOs.Sync;
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
/// Sync transport for registration plus invoice/payment business records.
/// Business changes are coalesced by stable entity ID and acknowledged by the server.
/// </summary>
public sealed class RegistrationSyncService : ISyncService
{
    private static readonly string[] RegistrationEntityTypes = ["User", "Company", "UserCompany", "Subscription"];
    private static readonly string[] BusinessEntityTypes = ["Invoice", "Purchase", "PurchaseReturn", "Payment", "Supplier", "Product", "StockMovement"];

    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly IAuthApiClient _authApi;
    private readonly IPendingRegistrationSecretStore _pendingSecrets;
    private readonly IConnectivityService _connectivity;
    private readonly IAppSessionContext _session;
    private readonly ILogger<RegistrationSyncService> _logger;
    private readonly ILicenseService? _licenses;
    private readonly IBusinessSyncApiClient? _businessApi;
    private readonly ISecureTokenStore? _tokens;
    private readonly object _gate = new();
    private bool _isSyncing;

    public RegistrationSyncService(
        IDbContextFactory<LocalAppDbContext> dbFactory,
        IAuthApiClient authApi,
        IPendingRegistrationSecretStore pendingSecrets,
        IConnectivityService connectivity,
        IAppSessionContext session,
        ILogger<RegistrationSyncService> logger,
        ILicenseService? licenses = null,
        IBusinessSyncApiClient? businessApi = null,
        ISecureTokenStore? tokens = null)
    {
        _dbFactory = dbFactory;
        _authApi = authApi;
        _pendingSecrets = pendingSecrets;
        _connectivity = connectivity;
        _session = session;
        _logger = logger;
        _licenses = licenses;
        _businessApi = businessApi;
        _tokens = tokens;
        _connectivity.StatusChanged += OnConnectivityChanged;
    }

    public bool IsSyncing => _isSyncing;
    public DateTime? LastSyncAt { get; private set; }
    public int PendingCount { get; private set; }
    public int FailedCount { get; private set; }

    public event EventHandler<SyncHistoryModel>? SyncCompleted;

    public async Task<bool> SyncNowAsync()
    {
        lock (_gate)
        {
            if (_isSyncing)
                return false;
            _isSyncing = true;
        }

        try
        {
            await RefreshCountsAsync();
            if (!_connectivity.IsOnline)
            {
                RaiseHistory(false, 0, "Offline — sync deferred until connectivity is restored.");
                return false;
            }

            await using var db = await _dbFactory.CreateDbContextAsync();
            var pending = await db.SyncQueue
                .Where(q => q.Status == (int)RecordSyncStatus.Pending || q.Status == (int)RecordSyncStatus.Failed)
                .OrderBy(q => q.CreatedAt)
                .ToListAsync();

            if (pending.Count == 0)
            {
                RaiseHistory(true, 0, "No pending sync items.");
                LastSyncAt = DateTime.UtcNow;
                return true;
            }

            var registrationItems = pending
                .Where(p => RegistrationEntityTypes.Contains(p.EntityType, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var succeeded = 0;
            var failed = 0;

            if (registrationItems.Count > 0)
            {
                var ok = await SyncRegistrationBatchAsync(db, registrationItems);
                if (ok) succeeded += registrationItems.Count;
                else failed += registrationItems.Count;
            }

            if (_licenses is not null)
            {
                try { await _licenses.SyncOnlineIfPossibleAsync(); }
                catch (Exception ex) { _logger.LogWarning(ex, "License sync during SyncNow failed."); }
            }

            var businessItems = pending
                .Where(p => BusinessEntityTypes.Contains(p.EntityType, StringComparer.OrdinalIgnoreCase)
                            && p.CompanyId == _session.CurrentCompanyId)
                .ToList();
            if (businessItems.Count > 0)
            {
                var (businessSucceeded, businessFailed) = await SyncBusinessBatchAsync(db, businessItems);
                succeeded += businessSucceeded;
                failed += businessFailed;
            }

            await db.SaveChangesAsync();
            await RefreshCountsAsync();
            LastSyncAt = DateTime.UtcNow;
            RaiseHistory(failed == 0, succeeded,
                failed == 0
                    ? $"Synced {succeeded} item(s)."
                    : $"Synced {succeeded}, failed {failed}.");
            return failed == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SyncNow failed.");
            try
            {
                await using var recoveryDb = await _dbFactory.CreateDbContextAsync();
                var interrupted = await recoveryDb.SyncQueue
                    .Where(q => q.Status == (int)RecordSyncStatus.Syncing)
                    .ToListAsync();
                foreach (var item in interrupted)
                {
                    item.Status = (int)RecordSyncStatus.Pending;
                    item.Error = "Synchronization was interrupted; retry is required.";
                }
                await recoveryDb.SaveChangesAsync();
                await RefreshCountsAsync();
            }
            catch (Exception recoveryError)
            {
                _logger.LogWarning(recoveryError, "Unable to recover interrupted sync queue items.");
            }
            RaiseHistory(false, 0, "Unable to synchronize. Please try again.");
            return false;
        }
        finally
        {
            lock (_gate) _isSyncing = false;
        }
    }

    public Task<bool> RetryFailedAsync() => SyncNowAsync();

    public async Task<List<SyncHistoryModel>> GetHistoryAsync()
    {
        await RefreshCountsAsync();
        return
        [
            new SyncHistoryModel
            {
                SyncedAt = LastSyncAt ?? DateTime.UtcNow,
                Success = FailedCount == 0,
                ItemsSynced = Math.Max(0, PendingCount == 0 ? 1 : 0),
                Module = "Registration / Billing",
                Message = PendingCount > 0
                    ? $"{PendingCount} pending, {FailedCount} failed."
                    : "Queue is clear."
            }
        ];
    }

    private async Task<bool> SyncRegistrationBatchAsync(LocalAppDbContext db, List<LocalSyncQueueEntity> items)
    {
        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.Status = (int)RecordSyncStatus.Syncing;
            item.LastAttemptAt = now;
        }
        await db.SaveChangesAsync();

        var payloadJson = items.Select(i => i.PayloadJson).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            MarkFailed(items, "Registration payload missing.");
            return false;
        }

        OfflineRegistrationPayload? meta;
        try
        {
            meta = JsonSerializer.Deserialize<OfflineRegistrationPayload>(payloadJson);
        }
        catch
        {
            MarkFailed(items, "Invalid registration payload.");
            return false;
        }

        if (meta is null || meta.ClientUserId == Guid.Empty)
        {
            MarkFailed(items, "Invalid registration identity.");
            return false;
        }

        var password = await _pendingSecrets.GetPendingPasswordAsync(meta.ClientUserId);
        if (string.IsNullOrWhiteSpace(password))
        {
            MarkFailed(items, "Pending registration password is missing from Secure Storage.");
            return false;
        }

        var request = new RegisterRequest
        {
            FullName = meta.FullName,
            Email = meta.Email,
            MobileNumber = meta.MobileNumber,
            Password = password,
            ConfirmPassword = password,
            CompanyName = meta.CompanyName,
            OwnerName = meta.OwnerName,
            CompanyEmail = meta.CompanyEmail,
            CompanyMobile = meta.CompanyMobile,
            Plan = meta.Plan,
            InstallationId = meta.InstallationId,
            DeviceId = meta.DeviceId,
            DeviceName = meta.DeviceName,
            Platform = meta.Platform,
            ClientUserId = meta.ClientUserId,
            ClientCompanyId = meta.ClientCompanyId,
            ClientUserCompanyId = meta.ClientUserCompanyId,
            ClientSubscriptionId = meta.ClientSubscriptionId
        };

        var result = await _authApi.RegisterAsync(request);
        if (!result.Success || result.Data is null)
        {
            // Network/server unavailable — keep Pending/Failed for retry (do not mark Synced).
            var status = result.IsNetworkError
                ? RecordSyncStatus.Pending
                : RecordSyncStatus.Failed;
            foreach (var item in items)
            {
                item.RetryCount++;
                item.LastAttemptAt = DateTime.UtcNow;
                item.Error = result.Error ?? "Registration sync failed.";
                item.Status = (int)(item.RetryCount >= 5 ? RecordSyncStatus.Failed : status);
            }
            return false;
        }

        // Prove stable IDs: server must echo the same client IDs.
        if (result.Data.UserId != meta.ClientUserId
            || result.Data.CompanyId != meta.ClientCompanyId
            || result.Data.SubscriptionId != meta.ClientSubscriptionId)
        {
            MarkFailed(items,
                $"Server ID mismatch. localUser={meta.ClientUserId} serverUser={result.Data.UserId}");
            return false;
        }

        var syncedAt = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.Status = (int)RecordSyncStatus.Synced;
            item.Error = null;
            item.SyncedAt = syncedAt;
            item.LastAttemptAt = syncedAt;
        }

        await _pendingSecrets.ClearPendingPasswordAsync(meta.ClientUserId);
        _ = _session; // auth context available for later ERP sync modules
        return true;
    }

    private static void MarkFailed(List<LocalSyncQueueEntity> items, string error)
    {
        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.RetryCount++;
            item.LastAttemptAt = now;
            item.Error = error;
            item.Status = (int)RecordSyncStatus.Failed;
        }
    }

    private async Task<(int Succeeded, int Failed)> SyncBusinessBatchAsync(
        LocalAppDbContext db,
        List<LocalSyncQueueEntity> items)
    {
        if (_businessApi is null || _tokens is null)
            return (0, items.Count);

        var accessToken = await _tokens.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            MarkRetriable(items, "Sign in online before synchronizing business data.", networkFailure: true);
            return (0, items.Count);
        }

        var latest = items
            .GroupBy(i => new { Type = i.EntityType.ToUpperInvariant(), i.EntityId })
            .Select(g => g.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id).First())
            .OrderBy(i => i.EntityType.Equals("Invoice", StringComparison.OrdinalIgnoreCase)
                          || i.EntityType.Equals("Purchase", StringComparison.OrdinalIgnoreCase) ? 0
                : i.EntityType.Equals("Payment", StringComparison.OrdinalIgnoreCase) ? 2 : 1)
            .ThenBy(i => i.CreatedAt)
            .ToList();

        foreach (var item in items)
        {
            item.Status = (int)RecordSyncStatus.Syncing;
            item.LastAttemptAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();

        var request = new BusinessSyncBatchRequest();
        foreach (var item in latest)
        {
            request.Items.Add(new BusinessSyncItemRequest
            {
                QueueId = item.Id,
                EntityType = item.EntityType,
                EntityId = item.EntityId,
                CompanyId = item.CompanyId!.Value,
                Operation = (SyncOperation)item.Operation,
                PayloadJson = await CurrentPayloadAsync(db, item),
                ClientUpdatedAtUtc = item.CreatedAt
            });
        }

        var result = await _businessApi.PushAsync(request, accessToken);
        if (!result.Success || result.Data is null)
        {
            MarkRetriable(items, result.Error ?? "Business sync failed.", result.IsNetworkError);
            return (0, items.Count);
        }

        var acknowledged = result.Data.Items
            .GroupBy(x => x.QueueId)
            .ToDictionary(group => group.Key, group => group.Last());
        var now = result.Data.ServerTimeUtc == default ? DateTime.UtcNow : result.Data.ServerTimeUtc;
        var succeeded = 0;
        var failed = 0;
        foreach (var group in items.GroupBy(i => new { Type = i.EntityType.ToUpperInvariant(), i.EntityId }))
        {
            var newest = group.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id).First();
            if (!acknowledged.TryGetValue(newest.Id, out var acknowledgement) || !acknowledgement.Success)
            {
                MarkRetriable(
                    group.ToList(),
                    acknowledgement?.Error ?? "Server did not acknowledge the business record.",
                    networkFailure: false);
                failed += group.Count();
                continue;
            }

            foreach (var item in group)
            {
                item.Status = (int)RecordSyncStatus.Synced;
                item.Error = null;
                item.SyncedAt = now;
                item.LastAttemptAt = now;
            }
            await MarkLocalRecordSyncedAsync(db, newest, now);
            succeeded += group.Count();
        }
        return (succeeded, failed);
    }

    private static async Task<string?> CurrentPayloadAsync(LocalAppDbContext db, LocalSyncQueueEntity item)
    {
        if ((SyncOperation)item.Operation == SyncOperation.Delete)
            return null;

        if (item.EntityType.Equals("Invoice", StringComparison.OrdinalIgnoreCase))
        {
            var row = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.EntityId && x.CompanyId == item.CompanyId);
            if (row is null) return null;
            var invoiceItems = JsonSerializer.Deserialize<List<InvoiceLineItem>>(row.ItemsJson) ?? [];
            var subtotal = invoiceItems.Sum(i => i.LineTotal);
            var total = subtotal + invoiceItems.Sum(i => i.TaxAmount)
                        + subtotal * row.TaxPct / 100 - subtotal * row.DiscountPct / 100;
            return JsonSerializer.Serialize(new
            {
                row.Id, row.CompanyId, row.InvoiceNumber, row.CustomerId, row.CustomerName,
                row.InvoiceDate, row.DueDate, row.ItemsJson, row.DiscountPct, row.TaxPct,
                row.PaymentMethod, row.Notes, row.Status, row.PaidAmount,
                GrandTotal = total, row.UpdatedAtUtc
            });
        }

        if (item.EntityType.Equals("Payment", StringComparison.OrdinalIgnoreCase))
        {
            var row = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.EntityId && x.CompanyId == item.CompanyId);
            if (row is null) return null;
            return JsonSerializer.Serialize(new
            {
                row.Id, row.CompanyId, row.PaymentNumber, row.PartyId, row.PartyName,
                row.IsSupplier, row.InvoiceId, row.InvoiceNumber, row.Amount, row.Method,
                row.PaymentDate, row.Reference, row.Notes, row.Status, row.UpdatedAtUtc
            });
        }

        if (item.EntityType.Equals("Purchase", StringComparison.OrdinalIgnoreCase))
        {
            var row = await db.Purchases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.EntityId && x.CompanyId == item.CompanyId);
            return row is null ? null : JsonSerializer.Serialize(LocalPurchaseService.Payload(row));
        }
        if (item.EntityType.Equals("PurchaseReturn", StringComparison.OrdinalIgnoreCase))
        {
            var row = await db.PurchaseReturns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.EntityId && x.CompanyId == item.CompanyId);
            return row is null ? null : JsonSerializer.Serialize(LocalReturnService.PurchasePayload(row));
        }
        if (item.EntityType.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
        {
            var row = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.EntityId && x.CompanyId == item.CompanyId);
            return row is null ? null : JsonSerializer.Serialize(new { row.Id,row.CompanyId,row.Name,row.Email,row.Phone,row.Address,row.TaxNumber,row.Notes,row.IsActive,row.UpdatedAtUtc });
        }
        if (item.EntityType.Equals("Product", StringComparison.OrdinalIgnoreCase))
        {
            var row = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.EntityId && x.CompanyId == item.CompanyId);
            return row is null ? null : JsonSerializer.Serialize(new { row.Id,row.CompanyId,row.Name,row.SKU,row.Barcode,row.Category,row.Brand,row.Unit,row.PurchasePrice,row.SellingPrice,row.TaxPercent,row.Stock,row.MinimumStock,row.Status,row.UpdatedAtUtc });
        }
        if (item.EntityType.Equals("StockMovement", StringComparison.OrdinalIgnoreCase))
        {
            var row = await db.StockMovements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.EntityId && x.CompanyId == item.CompanyId);
            return row is null ? null : JsonSerializer.Serialize(new { row.Id,row.CompanyId,row.ProductId,row.ProductName,row.SKU,row.Type,row.Quantity,row.StockBefore,row.StockAfter,row.Reference,row.Notes,row.CreatedBy,row.UpdatedAtUtc });
        }

        return item.PayloadJson;
    }

    private static async Task MarkLocalRecordSyncedAsync(
        LocalAppDbContext db,
        LocalSyncQueueEntity item,
        DateTime syncedAt)
    {
        if (item.EntityType.Equals("Invoice", StringComparison.OrdinalIgnoreCase))
        {
            var row = await db.Invoices.FirstOrDefaultAsync(x => x.Id == item.EntityId);
            if (row is not null)
            {
                row.ServerId = row.Id;
                row.SyncStatus = (int)RecordSyncStatus.Synced;
                row.SyncError = null;
                row.LastSyncedAtUtc = syncedAt;
            }
        }
        else if (item.EntityType.Equals("Payment", StringComparison.OrdinalIgnoreCase))
        {
            var row = await db.Payments.FirstOrDefaultAsync(x => x.Id == item.EntityId);
            if (row is not null)
            {
                row.ServerId = row.Id;
                row.SyncStatus = (int)RecordSyncStatus.Synced;
                row.SyncError = null;
                row.LastSyncedAtUtc = syncedAt;
            }
        }
        else if (item.EntityType.Equals("Purchase", StringComparison.OrdinalIgnoreCase))
        {
            var row = await db.Purchases.FirstOrDefaultAsync(x => x.Id == item.EntityId);
            if (row is not null) { row.ServerId=row.Id; row.SyncStatus=(int)RecordSyncStatus.Synced; row.SyncError=null; row.LastSyncedAtUtc=syncedAt; }
        }
        else if (item.EntityType.Equals("PurchaseReturn", StringComparison.OrdinalIgnoreCase))
        {
            var row = await db.PurchaseReturns.FirstOrDefaultAsync(x => x.Id == item.EntityId);
            if (row is not null) { row.ServerId=row.Id; row.SyncStatus=(int)RecordSyncStatus.Synced; row.SyncError=null; row.LastSyncedAtUtc=syncedAt; }
        }
        else if (item.EntityType.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
        {
            var row = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == item.EntityId);
            if (row is not null) { row.ServerId=row.Id; row.SyncStatus=(int)RecordSyncStatus.Synced; row.SyncError=null; row.LastSyncedAtUtc=syncedAt; }
        }
        else if (item.EntityType.Equals("Product", StringComparison.OrdinalIgnoreCase))
        {
            var row = await db.Products.FirstOrDefaultAsync(x => x.Id == item.EntityId);
            if (row is not null) { row.ServerId=row.Id; row.SyncStatus=(int)RecordSyncStatus.Synced; row.SyncError=null; row.LastSyncedAtUtc=syncedAt; }
        }
        else if (item.EntityType.Equals("StockMovement", StringComparison.OrdinalIgnoreCase))
        {
            var row = await db.StockMovements.FirstOrDefaultAsync(x => x.Id == item.EntityId);
            if (row is not null) { row.ServerId=row.Id; row.SyncStatus=(int)RecordSyncStatus.Synced; row.SyncError=null; row.LastSyncedAtUtc=syncedAt; }
        }
    }

    private static void MarkRetriable(
        List<LocalSyncQueueEntity> items,
        string error,
        bool networkFailure)
    {
        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.RetryCount++;
            item.LastAttemptAt = now;
            item.Error = error;
            item.Status = (int)(networkFailure && item.RetryCount < 5
                ? RecordSyncStatus.Pending
                : RecordSyncStatus.Failed);
        }
    }

    private async Task RefreshCountsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        PendingCount = await db.SyncQueue.CountAsync(q => q.Status == (int)RecordSyncStatus.Pending);
        FailedCount = await db.SyncQueue.CountAsync(q => q.Status == (int)RecordSyncStatus.Failed);
    }

    private void OnConnectivityChanged(object? sender, ConnectivityStatus status)
    {
        if (status is ConnectivityStatus.Online or ConnectivityStatus.Synced or ConnectivityStatus.PendingSync)
            _ = SyncNowAsync();
    }

    private void RaiseHistory(bool success, int items, string message)
        => SyncCompleted?.Invoke(this, new SyncHistoryModel
        {
            SyncedAt = DateTime.UtcNow,
            Success = success,
            ItemsSynced = items,
            Module = "Registration / Billing",
            Message = message
        });
}
