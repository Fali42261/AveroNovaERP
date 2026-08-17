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
/// Real sync transport for pending local registration (and later ERP) queue items.
/// Calls POST /api/auth/register for offline registration creates — not a UI mock.
/// </summary>
public sealed class RegistrationSyncService : ISyncService
{
    private static readonly string[] RegistrationEntityTypes = ["User", "Company", "UserCompany", "Subscription"];

    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly IAuthApiClient _authApi;
    private readonly IPendingRegistrationSecretStore _pendingSecrets;
    private readonly IConnectivityService _connectivity;
    private readonly IAppSessionContext _session;
    private readonly ILogger<RegistrationSyncService> _logger;
    private readonly ILicenseService? _licenses;
    private readonly object _gate = new();
    private bool _isSyncing;

    public RegistrationSyncService(
        IDbContextFactory<LocalAppDbContext> dbFactory,
        IAuthApiClient authApi,
        IPendingRegistrationSecretStore pendingSecrets,
        IConnectivityService connectivity,
        IAppSessionContext session,
        ILogger<RegistrationSyncService> logger,
        ILicenseService? licenses = null)
    {
        _dbFactory = dbFactory;
        _authApi = authApi;
        _pendingSecrets = pendingSecrets;
        _connectivity = connectivity;
        _session = session;
        _logger = logger;
        _licenses = licenses;
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

            // Business-module APIs are not on the server yet. Keep Pending — never fake Synced.
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
                Module = "Registration",
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
            Module = "Registration",
            Message = message
        });
}
