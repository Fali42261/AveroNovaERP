using AveroNova.Application.Common;
using AveroNova.Application.DTOs.Auth;
using System.Security.Claims;

namespace AveroNova.Application.Interfaces.Auth;

public interface IAuthService
{
    Task<ApiResult<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<LoginResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> LogoutAsync(ClaimsPrincipal principal, LogoutRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<MeResponse>> GetMeAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc, string Jti) CreateAccessToken(
        Guid userId,
        Guid companyId,
        Guid sessionId,
        IEnumerable<string> roles);

    int AccessTokenLifetimeSeconds { get; }
}

public interface IRefreshTokenService
{
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
    bool Matches(string refreshToken, string refreshTokenHash);
}

public interface IAuthAuditLogger
{
    void LoginSuccess(Guid userId, Guid companyId, Guid sessionId, string deviceId);
    void LoginFailure(string email, string reason);
    void TokenRefresh(Guid userId, Guid sessionId);
    void Logout(Guid userId, Guid sessionId);
    void SessionRevoked(Guid userId, Guid sessionId, string reason);
}

public interface ILoginAttemptProtector
{
    bool IsBlocked(string key);
    void RecordFailure(string key);
    void Reset(string key);
}
