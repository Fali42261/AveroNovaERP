using AveroNova.Domain.Enums;

namespace AveroNova.App.UI.Services.Api;

public sealed class CompanySyncRequest
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? MobileNumber { get; set; }
    public long SyncVersion { get; set; }
}

public sealed class CompanySyncResponse
{
    public Guid Id { get; set; }
    public long SyncVersion { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public interface ICompanyApiClient
{
    Task<ApiCallResult<CompanySyncResponse>> PushAsync(
        CompanySyncRequest request,
        SyncOperation operation,
        string accessToken,
        CancellationToken cancellationToken = default);
}

public sealed class CompanyApiClient : ICompanyApiClient
{
    private readonly IApiClient _api;

    public CompanyApiClient(IApiClient api) => _api = api;

    public Task<ApiCallResult<CompanySyncResponse>> PushAsync(
        CompanySyncRequest request,
        SyncOperation operation,
        string accessToken,
        CancellationToken cancellationToken = default)
        => operation switch
        {
            SyncOperation.Create => _api.PostAsync<CompanySyncResponse>(
                "api/companies", request, accessToken, cancellationToken),
            SyncOperation.Update => _api.PutAsync<CompanySyncResponse>(
                $"api/companies/{request.Id:D}", request, accessToken, cancellationToken),
            _ => Task.FromResult(ApiCallResult<CompanySyncResponse>.Fail(
                400, $"Company sync operation '{operation}' is not supported."))
        };
}
