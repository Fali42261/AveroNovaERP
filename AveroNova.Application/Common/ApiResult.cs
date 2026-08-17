namespace AveroNova.Application.Common;

public sealed class ApiResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public int StatusCode { get; init; } = 200;
    public IReadOnlyList<string>? Errors { get; init; }

    public static ApiResult Ok() => new() { Success = true, StatusCode = 200 };

    public static ApiResult Fail(string error, int statusCode = 400, IReadOnlyList<string>? errors = null)
        => new() { Success = false, Error = error, StatusCode = statusCode, Errors = errors };
}

public sealed class ApiResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
    public int StatusCode { get; init; } = 200;
    public IReadOnlyList<string>? Errors { get; init; }

    public static ApiResult<T> Ok(T data) => new() { Success = true, Data = data, StatusCode = 200 };

    public static ApiResult<T> Fail(string error, int statusCode = 400, IReadOnlyList<string>? errors = null)
        => new() { Success = false, Error = error, StatusCode = statusCode, Errors = errors };
}
