using AveroNova.Application.DTOs.License;
using AveroNova.App.UI.Services.Api;

namespace AveroNova.App.UI.Services.License;

public interface ILicenseApiClient
{
    Task<ApiCallResult<LicenseStatusResponse>> InitializeAsync(LicenseInitializeRequest request, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<ApiCallResult<LicenseStatusResponse>> GetStatusAsync(string deviceId, string bearerToken, CancellationToken cancellationToken = default);
    Task<ApiCallResult<LicenseStatusResponse>> ValidateAsync(LicenseValidateRequest request, string bearerToken, CancellationToken cancellationToken = default);
    Task<ApiCallResult<LicenseStatusResponse>> SyncAsync(LicenseSyncRequest request, string bearerToken, CancellationToken cancellationToken = default);
}

public sealed class LicenseApiClient : ILicenseApiClient
{
    private readonly IApiClient _api;

    public LicenseApiClient(IApiClient api) => _api = api;

    public Task<ApiCallResult<LicenseStatusResponse>> InitializeAsync(
        LicenseInitializeRequest request,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
        => _api.PostAsync<LicenseStatusResponse>("api/license/initialize", request, bearerToken, cancellationToken);

    public Task<ApiCallResult<LicenseStatusResponse>> GetStatusAsync(
        string deviceId,
        string bearerToken,
        CancellationToken cancellationToken = default)
        => _api.GetAsync<LicenseStatusResponse>(
            $"api/license/status?deviceId={Uri.EscapeDataString(deviceId)}",
            bearerToken,
            cancellationToken);

    public Task<ApiCallResult<LicenseStatusResponse>> ValidateAsync(
        LicenseValidateRequest request,
        string bearerToken,
        CancellationToken cancellationToken = default)
        => _api.PostAsync<LicenseStatusResponse>("api/license/validate", request, bearerToken, cancellationToken);

    public Task<ApiCallResult<LicenseStatusResponse>> SyncAsync(
        LicenseSyncRequest request,
        string bearerToken,
        CancellationToken cancellationToken = default)
        => _api.PostAsync<LicenseStatusResponse>("api/license/sync", request, bearerToken, cancellationToken);
}
