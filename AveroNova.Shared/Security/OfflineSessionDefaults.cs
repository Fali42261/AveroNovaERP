namespace AveroNova.Shared.Security;

/// <summary>
/// Offline session policy knobs. Values are configuration-driven — not unlimited JWT lifetime.
/// </summary>
public static class OfflineSessionDefaults
{
    /// <summary>Maximum age of a local authenticated session before online re-auth is required.</summary>
    public static readonly TimeSpan OfflineSessionMaxAge = TimeSpan.FromDays(14);

    public const string SecureAccessTokenKey = "averonova.auth.access_token";
    public const string SecureRefreshTokenKey = "averonova.auth.refresh_token";
    public const string SecureTokenExpiryKey = "averonova.auth.access_expires_utc";
    public const string SecureSessionIdKey = "averonova.auth.session_id";
    public const string SecurePendingRegistrationPasswordPrefix = "averonova.pending.reg.pwd.";
    public const string SecureCredentialHashPrefix = "averonova.cred.hash.";
    public const string SecureCredentialEmailPrefix = "averonova.cred.email.";
    public const string SecureLicenseAnchorKey = "averonova.license.anchor";
}
