namespace AveroNova.App.UI.Services.Security;

public interface ISecureTokenStore
{
    Task SetAccessTokenAsync(string token, DateTime expiresUtc);
    Task SetRefreshTokenAsync(string token);
    Task SetSessionIdAsync(Guid sessionId);
    Task SetPasswordRecoveryKeyAsync(string recoveryKey);
    Task<string?> GetAccessTokenAsync();
    Task<string?> GetRefreshTokenAsync();
    Task<string?> GetPasswordRecoveryKeyAsync();
    Task<DateTime?> GetAccessTokenExpiryAsync();
    Task<Guid?> GetSessionIdAsync();
    Task ClearAsync();
}
