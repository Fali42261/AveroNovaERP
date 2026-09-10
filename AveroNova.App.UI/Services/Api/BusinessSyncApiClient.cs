using AveroNova.Application.DTOs.Sync;

namespace AveroNova.App.UI.Services.Api;

public interface IBusinessSyncApiClient
{
    Task<ApiCallResult<BusinessSyncBatchResponse>> PushAsync(
        BusinessSyncBatchRequest request,
        string accessToken,
        CancellationToken cancellationToken = default);
}

public sealed class BusinessSyncApiClient : IBusinessSyncApiClient
{
    private readonly IApiClient _api;

    public BusinessSyncApiClient(IApiClient api) => _api = api;

    public Task<ApiCallResult<BusinessSyncBatchResponse>> PushAsync(
        BusinessSyncBatchRequest request,
        string accessToken,
        CancellationToken cancellationToken = default)
        => _api.PostAsync<BusinessSyncBatchResponse>(
            "api/sync/business/push", request, accessToken, cancellationToken);
}
