namespace AveroNova.Shared.Security;

/// <summary>
/// Offline session policy knobs. Values are configuration-driven — not unlimited JWT lifetime.
/// </summary>
public static class OfflineSessionDefaults
{
    /// <summary>Maximum age of a local authenticated session before online re-auth is required.</summary>
    public static readonly TimeSpan OfflineSessionMaxAge = TimeSpan.FromDays(14);

    /// <summary>Logout after this much user inactivity on desktop or mobile.</summary>
    public static readonly TimeSpan InactivityTimeout = TimeSpan.FromMinutes(15);

    public const string SecureAccessTokenKey = "averonova.auth.access_token";
    public const string SecureRefreshTokenKey = "averonova.auth.refresh_token";
    public const string SecureTokenExpiryKey = "averonova.auth.access_expires_utc";
    public const string SecureSessionIdKey = "averonova.auth.session_id";
    public const string SecurePasswordRecoveryKey = "averonova.auth.password_recovery";
    public const string SecurePendingRegistrationPasswordPrefix = "averonova.pending.reg.pwd.";
    public const string SecureCredentialHashPrefix = "averonova.cred.hash.";
    public const string SecureCredentialEmailPrefix = "averonova.cred.email.";
    public const string SecureLicenseAnchorKey = "averonova.license.anchor";
}
