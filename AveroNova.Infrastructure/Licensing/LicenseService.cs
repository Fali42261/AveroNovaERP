using System.Security.Claims;
using AveroNova.Application.Common;
using AveroNova.Application.DTOs.License;
using AveroNova.Application.Interfaces.License;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Domain.Licensing;
using AveroNova.Infrastructure.Auth;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Licensing;

public sealed class LicenseService : ILicenseService
{
    private readonly AppDbContext _db;

    public LicenseService(AppDbContext db) => _db = db;

    public async Task<ApiResult<LicenseStatusResponse>> InitializeAsync(
        LicenseInitializeRequest request,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default)
    {
        var deviceId = NormalizeDeviceId(request.DeviceId);
        if (deviceId is null)
            return ApiResult<LicenseStatusResponse>.Fail("DeviceId is required.", 400);

        var userId = TryGetUserId(principal);
        var companyId = TryGetCompanyId(principal);
        if (userId is Guid uid && companyId is Guid cid)
        {
            var membershipOk = await UserBelongsToCompanyAsync(uid, cid, cancellationToken);
            if (!membershipOk)
                return ApiResult<LicenseStatusResponse>.Fail("You do not have access.", 403);
        }
        else
        {
            userId = null;
            companyId = null;
        }

        var now = DateTime.UtcNow;
        var license = await FindByDeviceIdAsync(deviceId, cancellationToken);

        if (license is null)
        {
            license = await FindByClientLicenseIdAsync(request.ClientLicenseId, cancellationToken);
        }

        if (license is null)
        {
            var start = ResolveTrialStart(now, request.ClientTrialStartDateUtc);
            license = License.StartStarterTrial(deviceId, start, userId, companyId, request.ClientLicenseId);
            RefreshAndTouch(license, now, validated: true, synced: true);
            _db.Licenses.Add(license);
        }
        else
        {
            if (!TryAuthorizeExisting(license, userId, out var error, out var status))
                return ApiResult<LicenseStatusResponse>.Fail(error!, status);

            BindIfUnbound(license, userId, companyId, now);
            RefreshAndTouch(license, now, validated: true, synced: true);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ApiResult<LicenseStatusResponse>.Ok(ToResponse(license, now));
    }

    public Task<ApiResult<LicenseStatusResponse>> GetStatusAsync(
        ClaimsPrincipal principal,
        string deviceId,
        CancellationToken cancellationToken = default)
        => LoadAuthorizedAsync(principal, deviceId, persist: false, cancellationToken);

    public Task<ApiResult<LicenseStatusResponse>> ValidateAsync(
        LicenseValidateRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
        => LoadAuthorizedAsync(principal, request.DeviceId, persist: true, cancellationToken);

    public Task<ApiResult<LicenseStatusResponse>> SyncAsync(
        LicenseSyncRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
        => LoadAuthorizedAsync(principal, request.DeviceId, persist: true, cancellationToken);

    private async Task<ApiResult<LicenseStatusResponse>> LoadAuthorizedAsync(
        ClaimsPrincipal principal,
        string deviceId,
        bool persist,
        CancellationToken cancellationToken)
    {
        var userId = TryGetUserId(principal);
        var companyId = TryGetCompanyId(principal);
        if (userId is null || companyId is null)
            return ApiResult<LicenseStatusResponse>.Fail("Authentication is required.", 401);

        if (!await UserBelongsToCompanyAsync(userId.Value, companyId.Value, cancellationToken))
            return ApiResult<LicenseStatusResponse>.Fail("You do not have access.", 403);

        var normalizedDevice = NormalizeDeviceId(deviceId);
        var now = DateTime.UtcNow;

        License? license = null;
        if (normalizedDevice is not null)
            license = await FindByDeviceIdAsync(normalizedDevice, cancellationToken);

        license ??= await _db.Licenses
            .FirstOrDefaultAsync(
                l => l.UserId == userId && l.CompanyId == companyId && !l.IsDeleted,
                cancellationToken);

        if (license is null)
            return ApiResult<LicenseStatusResponse>.Fail("License was not found. Initialize the license first.", 404);

        if (!TryAuthorizeExisting(license, userId, out var error, out var status))
            return ApiResult<LicenseStatusResponse>.Fail(error!, status);

        BindIfUnbound(license, userId, companyId, now);
        RefreshAndTouch(license, now, validated: persist, synced: persist);

        if (persist || _db.Entry(license).State == EntityState.Modified)
            await _db.SaveChangesAsync(cancellationToken);

        return ApiResult<LicenseStatusResponse>.Ok(ToResponse(license, now));
    }

    private async Task<License?> FindByClientLicenseIdAsync(Guid? clientLicenseId, CancellationToken cancellationToken)
    {
        if (clientLicenseId is not Guid id || id == Guid.Empty)
            return null;

        return await _db.Licenses.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, cancellationToken);
    }

    private static DateTime ResolveTrialStart(DateTime serverUtc, DateTime? clientTrialStartUtc)
    {
        if (clientTrialStartUtc is not DateTime clientStart)
            return serverUtc;

        var start = clientStart.Kind == DateTimeKind.Utc
            ? clientStart
            : DateTime.SpecifyKind(clientStart.ToUniversalTime(), DateTimeKind.Utc);

        // Honor an earlier offline start so used trial days are not reset. Never accept a future start.
        return start < serverUtc ? start : serverUtc;
    }

    private async Task<License?> FindByDeviceIdAsync(string deviceId, CancellationToken cancellationToken)
        => await _db.Licenses.FirstOrDefaultAsync(l => l.DeviceId == deviceId && !l.IsDeleted, cancellationToken);

    private async Task<bool> UserBelongsToCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken)
        => await _db.UserCompanies.AnyAsync(
            uc => uc.UserId == userId && uc.CompanyId == companyId && uc.IsActive && !uc.IsDeleted,
            cancellationToken);

    private static bool TryAuthorizeExisting(License license, Guid? userId, out string? error, out int status)
    {
        error = null;
        status = 200;
        if (license.UserId is Guid bound && userId is Guid caller && bound != caller)
        {
            error = "You do not have access.";
            status = 403;
            return false;
        }

        return true;
    }

    private static void BindIfUnbound(License license, Guid? userId, Guid? companyId, DateTime utcNow)
    {
        if (license.UserId is not null || userId is null || companyId is null)
            return;

        license.UserId = userId;
        license.CompanyId = companyId;
        license.UpdatedAt = utcNow;
    }

    private static void RefreshAndTouch(License license, DateTime utcNow, bool validated, bool synced)
    {
        var resolved = LicenseEvaluator.ResolveStatus(
            license.Status,
            license.IsTrial,
            license.TrialEndDateUtc,
            license.ExpiryDateUtc,
            utcNow);

        if (resolved != license.Status)
        {
            license.Status = resolved;
            license.UpdatedAt = utcNow;
        }

        if (validated)
            license.LastValidatedAtUtc = utcNow;
        if (synced)
            license.LastSyncedAtUtc = utcNow;
    }

    private static LicenseStatusResponse ToResponse(License license, DateTime serverTimeUtc)
        => new()
        {
            LicenseId = license.Id,
            Plan = license.Plan,
            Status = license.Status,
            IsTrial = license.IsTrial,
            TrialStartDateUtc = license.TrialStartDateUtc,
            TrialEndDateUtc = license.TrialEndDateUtc,
            ExpiryDateUtc = license.ExpiryDateUtc,
            ServerTimeUtc = serverTimeUtc,
            LastValidatedAtUtc = license.LastValidatedAtUtc,
            LastSyncedAtUtc = license.LastSyncedAtUtc
        };

    private static string? NormalizeDeviceId(string? deviceId)
    {
        var value = deviceId?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static Guid? TryGetUserId(ClaimsPrincipal? principal)
    {
        var raw = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal?.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static Guid? TryGetCompanyId(ClaimsPrincipal? principal)
    {
        var raw = principal?.FindFirstValue(JwtTokenService.CompanyIdClaim);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
