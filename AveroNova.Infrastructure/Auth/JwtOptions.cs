namespace AveroNova.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "AveroNova";
    public string Audience { get; set; } = "AveroNova.Clients";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>
    /// Signing key from configuration / environment / user-secrets. Never hardcode in source.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;
}

public sealed class AuthSecurityOptions
{
    public const string SectionName = "AuthSecurity";

    public int MaxFailedAttempts { get; set; } = 8;
    public int LockoutMinutes { get; set; } = 15;
}
