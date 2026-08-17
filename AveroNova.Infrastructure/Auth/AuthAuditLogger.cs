using AveroNova.Application.Interfaces.Auth;
using Microsoft.Extensions.Logging;

namespace AveroNova.Infrastructure.Auth;

public sealed class AuthAuditLogger : IAuthAuditLogger
{
    private readonly ILogger<AuthAuditLogger> _logger;

    public AuthAuditLogger(ILogger<AuthAuditLogger> logger) => _logger = logger;

    public void LoginSuccess(Guid userId, Guid companyId, Guid sessionId, string deviceId)
        => _logger.LogInformation(
            "AUTH LoginSuccess UserId={UserId} CompanyId={CompanyId} SessionId={SessionId} DeviceId={DeviceId}",
            userId, companyId, sessionId, deviceId);

    public void LoginFailure(string email, string reason)
        => _logger.LogWarning("AUTH LoginFailure EmailHash={EmailHash} Reason={Reason}", HashEmail(email), reason);

    public void TokenRefresh(Guid userId, Guid sessionId)
        => _logger.LogInformation("AUTH TokenRefresh UserId={UserId} SessionId={SessionId}", userId, sessionId);

    public void Logout(Guid userId, Guid sessionId)
        => _logger.LogInformation("AUTH Logout UserId={UserId} SessionId={SessionId}", userId, sessionId);

    public void SessionRevoked(Guid userId, Guid sessionId, string reason)
        => _logger.LogWarning(
            "AUTH SessionRevoked UserId={UserId} SessionId={SessionId} Reason={Reason}",
            userId, sessionId, reason);

    private static string HashEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "empty";
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..12];
    }
}
