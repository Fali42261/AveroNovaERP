using System.Security.Claims;
using AveroNova.Application.Common;
using AveroNova.Application.DTOs.License;

namespace AveroNova.Application.Interfaces.License;

public interface ILicenseService
{
    Task<ApiResult<LicenseStatusResponse>> InitializeAsync(
        LicenseInitializeRequest request,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default);

    Task<ApiResult<LicenseStatusResponse>> GetStatusAsync(
        ClaimsPrincipal principal,
        string deviceId,
        CancellationToken cancellationToken = default);

    Task<ApiResult<LicenseStatusResponse>> ValidateAsync(
        LicenseValidateRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<ApiResult<LicenseStatusResponse>> SyncAsync(
        LicenseSyncRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
