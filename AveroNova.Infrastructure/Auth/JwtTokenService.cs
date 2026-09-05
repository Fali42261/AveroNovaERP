using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AveroNova.Application.Interfaces.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AveroNova.Infrastructure.Auth;

public sealed class JwtTokenService : IJwtTokenService
{
    public const string CompanyIdClaim = "company_id";
    public const string SessionIdClaim = "session_id";

    private readonly JwtOptions _options;
    private readonly SigningCredentials _credentials;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
            throw new InvalidOperationException(
                "Jwt:SigningKey must be configured (min 32 chars) via environment/user-secrets/appsettings.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public int AccessTokenLifetimeSeconds => Math.Max(60, _options.AccessTokenMinutes * 60);

    public (string Token, DateTime ExpiresAtUtc, string Jti) CreateAccessToken(
        Guid userId,
        Guid companyId,
        Guid sessionId,
        IEnumerable<string> roles)
    {
        var jti = Guid.NewGuid().ToString("N");
        var expires = DateTime.UtcNow.AddSeconds(AccessTokenLifetimeSeconds);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(CompanyIdClaim, companyId.ToString()),
            new(SessionIdClaim, sessionId.ToString())
        };

        foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: _credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires, jti);
    }
}
