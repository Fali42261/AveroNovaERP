using AveroNova.Application.DTOs.License;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.License;
using AveroNova.App.UI.Services.Security;
using AveroNova.Domain.Constants;
using AveroNova.Domain.Enums;
using AveroNova.Domain.Licensing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AveroNova.App.UI.Services;

public sealed class LicenseService : ILicenseService
{
    public const string FirstActivationRequiredMessage = "Internet connection is required for first-time activation.";
    public const string OfflineBannerMessage = "Offline — changes will sync when connection is restored.";

    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly ILicenseApiClient _api;
    private readonly IInstallationService _installation;
    private readonly IConnectivityService _connectivity;
    private readonly ISecureTokenStore _tokens;
    private readonly ILicenseAnchorStore _anchor;
    private readonly ILogger<LicenseService> _logger;

    public LicenseService(
        IDbContextFactory<LocalAppDbContext> dbFactory,
        ILicenseApiClient api,
        IInstallationService installation,
        IConnectivityService connectivity,
        ISecureTokenStore tokens,
        ILicenseAnchorStore anchor,
        ILogger<LicenseService> logger)
    {
        _dbFactory = dbFactory;
        _api = api;
        _installation = installation;
        _connectivity = connectivity;
        _tokens = tokens;
        _anchor = anchor;
        _logger = logger;
    }

    public async Task<LicenseBootstrapStatus> EnsureActivatedAsync(CancellationToken cancellationToken = default)
    {
        await _installation.EnsureInitializedAsync(cancellationToken);
        await EnsureLocalTrialAsync(cancellationToken);

        try
        {
            await ValidateOnlineIfPossibleAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "License online validation failed; continuing with local license.");
        }

        var access = await GetAccessStateAsync(cancellationToken);
        return access.AllowsAccess ? LicenseBootstrapStatus.Ready : LicenseBootstrapStatus.Blocked;
    }

    public async Task<LicenseAccessState> GetAccessStateAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLocalTrialAsync(cancellationToken);
        var local = await GetLocalAsync(cancellationToken);
        if (local is null)
        {
            return new LicenseAccessState
            {
                AllowsAccess = false,
                NeedsFirstActivation = false,
                Status = LicenseStatus.Expired,
                Message = "License is unavailable on this device."
            };
        }

        var deviceNow = DateTime.UtcNow;
        var watermark = MaxTime(local.LastKnownTrustedTimeUtc, local.LastKnownServerTimeUtc);
        var rollback = LicenseEvaluator.IsObviousClockRollback(deviceNow, watermark);
        var effectiveNow = LicenseEvaluator.GetEffectiveUtc(deviceNow, watermark);
        var status = (LicenseStatus)local.Status;
        var allowed = LicenseEvaluator.AllowsAccess(
            status,
            local.IsTrial,
            local.TrialEndDateUtc,
            local.ExpiryDateUtc,
            effectiveNow);
        var resolved = LicenseEvaluator.ResolveStatus(
            status,
            local.IsTrial,
            local.TrialEndDateUtc,
            local.ExpiryDateUtc,
            effectiveNow);

        await PersistTrustedClockAsync(local.Id, deviceNow, watermark, rollback, cancellationToken);

        return new LicenseAccessState
        {
            AllowsAccess = allowed,
            NeedsFirstActivation = false,
            Status = resolved,
            Plan = local.Plan,
            IsTrial = local.IsTrial,
            TrialStartDateUtc = local.TrialStartDateUtc,
            TrialEndDateUtc = local.TrialEndDateUtc,
            RemainingTrialDays = local.IsTrial
                ? LicenseEvaluator.GetRemainingTrialDays(local.TrialEndDateUtc, effectiveNow)
                : 0,
            LastKnownServerTimeUtc = local.LastKnownServerTimeUtc,
            Message = allowed
                ? null
                : resolved == LicenseStatus.Expired
                    ? "The trial or license on this device has expired."
                    : "This license is not currently active."
        };
    }

    public async Task ValidateOnlineIfPossibleAsync(CancellationToken cancellationToken = default)
    {
        if (!_connectivity.IsOnline)
            return;

        await _installation.EnsureInitializedAsync(cancellationToken);
        var token = await _tokens.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            await InitializeOnlineAsync(cancellationToken);
            return;
        }

        var result = await _api.ValidateAsync(
            new LicenseValidateRequest { DeviceId = _installation.DeviceId },
            token,
            cancellationToken);

        if (result.Success && result.Data is not null)
        {
            await SaveLocalAsync(result.Data, serverAuthoritative: true, cancellationToken);
            return;
        }

        if (result.StatusCode is 404 or 401)
            await InitializeOnlineAsync(token, cancellationToken);
    }

    public async Task SyncOnlineIfPossibleAsync(CancellationToken cancellationToken = default)
    {
        if (!_connectivity.IsOnline)
            return;

        await _installation.EnsureInitializedAsync(cancellationToken);
        var token = await _tokens.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            await InitializeOnlineAsync(cancellationToken);
            return;
        }

        var result = await _api.SyncAsync(
            new LicenseSyncRequest { DeviceId = _installation.DeviceId },
            token,
            cancellationToken);

        if (result.Success && result.Data is not null)
            await SaveLocalAsync(result.Data, serverAuthoritative: true, cancellationToken);
        else if (result.StatusCode is 404 or 401)
            await InitializeOnlineAsync(token, cancellationToken);
    }

    public async Task<LicenseStatusResponse?> GetCachedStatusAsync(CancellationToken cancellationToken = default)
    {
        var local = await GetLocalAsync(cancellationToken);
        if (local is null)
            return null;

        return new LicenseStatusResponse
        {
            LicenseId = local.LicenseId,
            Plan = local.Plan,
            Status = (LicenseStatus)local.Status,
            IsTrial = local.IsTrial,
            TrialStartDateUtc = local.TrialStartDateUtc,
            TrialEndDateUtc = local.TrialEndDateUtc,
            ExpiryDateUtc = local.ExpiryDateUtc,
            ServerTimeUtc = local.LastKnownServerTimeUtc ?? DateTime.UtcNow,
            LastValidatedAtUtc = local.LastValidatedAtUtc,
            LastSyncedAtUtc = local.LastSyncedAtUtc
        };
    }

    private async Task EnsureLocalTrialAsync(CancellationToken cancellationToken)
    {
        await _installation.EnsureInitializedAsync(cancellationToken);
        var existing = await GetLocalAsync(cancellationToken);
        if (existing is not null)
        {
            await PersistAnchorAsync(existing);
            return;
        }

        var restored = await TryRestoreFromAnchorAsync(cancellationToken);
        if (restored)
            return;

        await CreateLocalTrialAsync(cancellationToken);
    }

    private async Task<bool> TryRestoreFromAnchorAsync(CancellationToken cancellationToken)
    {
        var anchor = await _anchor.LoadAsync();
        if (anchor is null)
            return false;
        if (!string.Equals(anchor.DeviceId, _installation.DeviceId, StringComparison.Ordinal))
            return false;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        db.Licenses.Add(new LocalLicenseEntity
        {
            Id = Guid.NewGuid(),
            LicenseId = anchor.LicenseId == Guid.Empty ? Guid.NewGuid() : anchor.LicenseId,
            DeviceId = _installation.DeviceId,
            Plan = string.IsNullOrWhiteSpace(anchor.Plan) ? LicenseConstants.StarterPlan : anchor.Plan,
            Status = anchor.Status == 0 ? (int)LicenseStatus.Trial : anchor.Status,
            IsTrial = anchor.IsTrial,
            TrialStartDateUtc = anchor.TrialStartDateUtc,
            TrialEndDateUtc = anchor.TrialEndDateUtc,
            ExpiryDateUtc = anchor.ExpiryDateUtc,
            LastKnownServerTimeUtc = anchor.LastKnownServerTimeUtc,
            LastKnownTrustedTimeUtc = LicenseEvaluator.AdvanceTrustedWatermark(now, anchor.LastKnownTrustedTimeUtc),
            IsServerAuthoritative = anchor.IsServerAuthoritative,
            UpdatedAtUtc = now
        });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task CreateLocalTrialAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var start = now;
        var end = start.AddDays(LicenseConstants.TrialDays);
        var row = new LocalLicenseEntity
        {
            Id = Guid.NewGuid(),
            LicenseId = Guid.NewGuid(),
            DeviceId = _installation.DeviceId,
            Plan = LicenseConstants.StarterPlan,
            Status = (int)LicenseStatus.Trial,
            IsTrial = true,
            TrialStartDateUtc = start,
            TrialEndDateUtc = end,
            ExpiryDateUtc = end,
            LastKnownTrustedTimeUtc = start,
            IsServerAuthoritative = false,
            UpdatedAtUtc = start
        };
        db.Licenses.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        await PersistAnchorAsync(row);
        _logger.LogInformation(
            "Created local Starter trial LicenseId={LicenseId} TrialEnd={TrialEnd}",
            row.LicenseId, row.TrialEndDateUtc);
    }

    private async Task<bool> InitializeOnlineAsync(CancellationToken cancellationToken)
        => await InitializeOnlineAsync(await _tokens.GetAccessTokenAsync(), cancellationToken);

    private async Task<bool> InitializeOnlineAsync(string? bearerToken, CancellationToken cancellationToken)
    {
        await _installation.EnsureInitializedAsync(cancellationToken);
        var local = await GetLocalAsync(cancellationToken);
        var result = await _api.InitializeAsync(
            new LicenseInitializeRequest
            {
                DeviceId = _installation.DeviceId,
                InstallationId = _installation.InstallationId,
                ClientLicenseId = local?.LicenseId,
                ClientTrialStartDateUtc = local?.TrialStartDateUtc,
                ClientTrialEndDateUtc = local?.TrialEndDateUtc
            },
            bearerToken,
            cancellationToken);

        if (!result.Success || result.Data is null)
        {
            _logger.LogWarning("License initialize failed; continuing locally. {Error}", result.Error);
            return false;
        }

        await SaveLocalAsync(result.Data, serverAuthoritative: true, cancellationToken);
        return true;
    }

    private async Task<LocalLicenseEntity?> GetLocalAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Licenses.OrderByDescending(x => x.UpdatedAtUtc).FirstOrDefaultAsync(cancellationToken);
    }

    private async Task SaveLocalAsync(LicenseStatusResponse response, bool serverAuthoritative, CancellationToken cancellationToken)
    {
        await _installation.EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Licenses.FirstOrDefaultAsync(x => x.LicenseId == response.LicenseId, cancellationToken)
                  ?? await db.Licenses.OrderByDescending(x => x.UpdatedAtUtc).FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;
        if (row is null)
        {
            row = new LocalLicenseEntity { Id = Guid.NewGuid(), LicenseId = response.LicenseId };
            db.Licenses.Add(row);
        }

        var previousTrusted = MaxTime(row.LastKnownTrustedTimeUtc, row.LastKnownServerTimeUtc);
        row.LicenseId = response.LicenseId;
        row.DeviceId = _installation.DeviceId;
        row.Plan = response.Plan;
        row.Status = (int)response.Status;
        row.IsTrial = response.IsTrial;
        row.TrialStartDateUtc = response.TrialStartDateUtc;
        row.TrialEndDateUtc = response.TrialEndDateUtc;
        row.ExpiryDateUtc = response.ExpiryDateUtc;
        row.LastValidatedAtUtc = response.LastValidatedAtUtc;
        row.LastSyncedAtUtc = response.LastSyncedAtUtc;
        row.LastKnownServerTimeUtc = response.ServerTimeUtc;
        row.LastKnownTrustedTimeUtc = LicenseEvaluator.AdvanceTrustedWatermark(
            now,
            MaxTime(previousTrusted, response.ServerTimeUtc));
        row.IsServerAuthoritative = serverAuthoritative;
        row.ClockRollbackDetected = LicenseEvaluator.IsObviousClockRollback(now, row.LastKnownTrustedTimeUtc);
        row.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        await PersistAnchorAsync(row);
    }

    private async Task PersistTrustedClockAsync(
        Guid localRowId,
        DateTime deviceUtc,
        DateTime? watermark,
        bool rollback,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Licenses.FirstOrDefaultAsync(x => x.Id == localRowId, cancellationToken);
        if (row is null)
            return;

        row.LastKnownTrustedTimeUtc = LicenseEvaluator.AdvanceTrustedWatermark(deviceUtc, watermark);
        row.ClockRollbackDetected = rollback;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await PersistAnchorAsync(row);
    }

    private async Task PersistAnchorAsync(LocalLicenseEntity row)
    {
        try
        {
            await _anchor.SaveAsync(new LicenseAnchor
            {
                DeviceId = row.DeviceId,
                LicenseId = row.LicenseId,
                Plan = row.Plan,
                Status = row.Status,
                IsTrial = row.IsTrial,
                TrialStartDateUtc = row.TrialStartDateUtc,
                TrialEndDateUtc = row.TrialEndDateUtc,
                ExpiryDateUtc = row.ExpiryDateUtc,
                LastKnownTrustedTimeUtc = row.LastKnownTrustedTimeUtc,
                LastKnownServerTimeUtc = row.LastKnownServerTimeUtc,
                IsServerAuthoritative = row.IsServerAuthoritative
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to persist license anchor.");
        }
    }

    private static DateTime? MaxTime(DateTime? left, DateTime? right)
    {
        if (left is not DateTime a || a.Year < 2)
            return right is DateTime b && b.Year >= 2 ? b : null;
        if (right is not DateTime other || other.Year < 2)
            return a;
        return a > other ? a : other;
    }
}
