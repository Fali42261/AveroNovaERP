using AveroNova.Application.DTOs.Auth;

namespace AveroNova.App.UI.Services.Api;

public interface IAuthApiClient
{
    Task<ApiCallResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<ApiCallResult<LoginResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);
    Task<ApiCallResult> LogoutAsync(LogoutRequest request, string accessToken, CancellationToken cancellationToken = default);
    Task<ApiCallResult<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<ApiCallResult<MeResponse>> MeAsync(string accessToken, CancellationToken cancellationToken = default);
}

public sealed class AuthApiClient : IAuthApiClient
{
    private readonly IApiClient _api;

    public AuthApiClient(IApiClient api) => _api = api;

    public Task<ApiCallResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        => _api.PostAsync<LoginResponse>("api/auth/login", request, cancellationToken: cancellationToken);

    public Task<ApiCallResult<LoginResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
        => _api.PostAsync<LoginResponse>("api/auth/refresh", request, cancellationToken: cancellationToken);

    public Task<ApiCallResult> LogoutAsync(LogoutRequest request, string accessToken, CancellationToken cancellationToken = default)
        => _api.PostAsync("api/auth/logout", request, accessToken, cancellationToken);

    public Task<ApiCallResult<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
        => _api.PostAsync<RegisterResponse>("api/auth/register", request, cancellationToken: cancellationToken);

    public Task<ApiCallResult<MeResponse>> MeAsync(string accessToken, CancellationToken cancellationToken = default)
        => _api.GetAsync<MeResponse>("api/auth/me", accessToken, cancellationToken);
}
