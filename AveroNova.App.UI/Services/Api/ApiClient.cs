using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AveroNova.App.UI.Services.Api;

public interface IApiClient
{
    Task<ApiCallResult<T>> GetAsync<T>(string relativeUrl, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<ApiCallResult<T>> PostAsync<T>(string relativeUrl, object? body, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<ApiCallResult> PostAsync(string relativeUrl, object? body, string? bearerToken = null, CancellationToken cancellationToken = default);
}

public class ApiCallResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string? Error { get; init; }
    public bool IsNetworkError { get; init; }

    public static ApiCallResult Ok(int statusCode) => new() { Success = true, StatusCode = statusCode };
    public static ApiCallResult Fail(int statusCode, string error, bool network = false)
        => new() { Success = false, StatusCode = statusCode, Error = error, IsNetworkError = network };
}

public class ApiCallResult<T> : ApiCallResult
{
    public T? Data { get; init; }

    public static ApiCallResult<T> Ok(T data, int statusCode)
        => new() { Success = true, StatusCode = statusCode, Data = data };

    public new static ApiCallResult<T> Fail(int statusCode, string error, bool network = false)
        => new() { Success = false, StatusCode = statusCode, Error = error, IsNetworkError = network };
}

public sealed class ApiEnvelope<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
}

public sealed class ApiClient : IApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;

    public ApiClient(HttpClient http, IOptions<ApiSettings> options)
    {
        _http = http;
        var baseUrl = options.Value.BaseUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException(
                "ApiSettings.BaseUrl is empty. Configure it in appsettings.Development.json / appsettings.Production.json.");
        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";
        _http.BaseAddress = new Uri(baseUrl);
        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.Value.TimeoutSeconds, 5, 120));
    }

    public Task<ApiCallResult<T>> GetAsync<T>(string relativeUrl, string? bearerToken = null, CancellationToken cancellationToken = default)
        => SendAsync<T>(HttpMethod.Get, relativeUrl, null, bearerToken, cancellationToken);

    public Task<ApiCallResult<T>> PostAsync<T>(string relativeUrl, object? body, string? bearerToken = null, CancellationToken cancellationToken = default)
        => SendAsync<T>(HttpMethod.Post, relativeUrl, body, bearerToken, cancellationToken);

    public async Task<ApiCallResult> PostAsync(string relativeUrl, object? body, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        var typed = await SendAsync<object>(HttpMethod.Post, relativeUrl, body, bearerToken, cancellationToken);
        return typed.Success
            ? ApiCallResult.Ok(typed.StatusCode)
            : ApiCallResult.Fail(typed.StatusCode, typed.Error ?? "Request failed.", typed.IsNetworkError);
    }

    private async Task<ApiCallResult<T>> SendAsync<T>(
        HttpMethod method,
        string relativeUrl,
        object? body,
        string? bearerToken,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, relativeUrl.TrimStart('/'));
            if (!string.IsNullOrWhiteSpace(bearerToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            if (body is not null)
                request.Content = JsonContent.Create(body, options: JsonOptions);

            using var response = await _http.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return response.IsSuccessStatusCode
                    ? ApiCallResult<T>.Ok(default!, status)
                    : ApiCallResult<T>.Fail(status, MapStatusError(status));
            }

            ApiEnvelope<T>? envelope = null;
            try
            {
                envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(raw, JsonOptions);
            }
            catch
            {
                // non-envelope payload
            }

            if (envelope is not null)
            {
                if (envelope.Success && envelope.Data is not null)
                    return ApiCallResult<T>.Ok(envelope.Data, status);
                if (envelope.Success && envelope.Data is null && response.IsSuccessStatusCode)
                    return ApiCallResult<T>.Ok(default!, status);
                return ApiCallResult<T>.Fail(status, envelope.Error ?? MapStatusError(status));
            }

            if (!response.IsSuccessStatusCode)
                return ApiCallResult<T>.Fail(status, MapStatusError(status));

            var direct = JsonSerializer.Deserialize<T>(raw, JsonOptions);
            return ApiCallResult<T>.Ok(direct!, status);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<T>.Fail(0, "Unable to connect to the server. Please try again.", network: true);
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<T>.Fail(0, "Unable to connect to the server. Please try again.", network: true);
        }
        catch (Exception)
        {
            return ApiCallResult<T>.Fail(0, "Unable to connect to the server. Please try again.", network: true);
        }
    }

    private static string MapStatusError(int status) => status switch
    {
        401 => "Invalid email or password.",
        403 => "You do not have access.",
        409 => "This installation is already registered. Please sign in instead.",
        429 => "Too many attempts. Please try again later.",
        _ => "Unable to complete the request. Please try again."
    };
}
