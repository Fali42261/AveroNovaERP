namespace AveroNova.App.UI.Services.Security;

/// <summary>
/// Holds the pending offline-registration password in Secure Storage only (never SQLite).
/// Cleared after successful sync to the server.
/// </summary>
public interface IPendingRegistrationSecretStore
{
    Task SetPendingPasswordAsync(Guid registrationUserId, string password);
    Task<string?> GetPendingPasswordAsync(Guid registrationUserId);
    Task ClearPendingPasswordAsync(Guid registrationUserId);
}
