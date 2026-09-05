using AveroNova.Shared.Security;

namespace AveroNova.App.UI.Services.Security;

/// <summary>
/// Platform secure storage for tokens — not ordinary SQLite.
/// </summary>
public sealed class MauiSecureTokenStore : ISecureTokenStore
{
    public Task SetAccessTokenAsync(string token, DateTime expiresUtc)
        => Task.WhenAll(
            SecureStorage.Default.SetAsync(OfflineSessionDefaults.SecureAccessTokenKey, token),
            SecureStorage.Default.SetAsync(OfflineSessionDefaults.SecureTokenExpiryKey, expiresUtc.ToUniversalTime().ToString("O")));

    public Task SetRefreshTokenAsync(string token)
        => SecureStorage.Default.SetAsync(OfflineSessionDefaults.SecureRefreshTokenKey, token);

    public Task SetSessionIdAsync(Guid sessionId)
        => SecureStorage.Default.SetAsync(OfflineSessionDefaults.SecureSessionIdKey, sessionId.ToString("D"));

    public Task<string?> GetAccessTokenAsync()
        => SecureStorage.Default.GetAsync(OfflineSessionDefaults.SecureAccessTokenKey);

    public Task<string?> GetRefreshTokenAsync()
        => SecureStorage.Default.GetAsync(OfflineSessionDefaults.SecureRefreshTokenKey);

    public async Task<DateTime?> GetAccessTokenExpiryAsync()
    {
        var raw = await SecureStorage.Default.GetAsync(OfflineSessionDefaults.SecureTokenExpiryKey);
        return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt.ToUniversalTime()
            : null;
    }

    public async Task<Guid?> GetSessionIdAsync()
    {
        var raw = await SecureStorage.Default.GetAsync(OfflineSessionDefaults.SecureSessionIdKey);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(OfflineSessionDefaults.SecureAccessTokenKey);
        SecureStorage.Default.Remove(OfflineSessionDefaults.SecureRefreshTokenKey);
        SecureStorage.Default.Remove(OfflineSessionDefaults.SecureTokenExpiryKey);
        SecureStorage.Default.Remove(OfflineSessionDefaults.SecureSessionIdKey);
        return Task.CompletedTask;
    }
}
