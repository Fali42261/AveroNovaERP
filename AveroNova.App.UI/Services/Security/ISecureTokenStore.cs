namespace AveroNova.App.UI.Services.Security;

public interface ISecureTokenStore
{
    Task SetAccessTokenAsync(string token, DateTime expiresUtc);
    Task SetRefreshTokenAsync(string token);
    Task SetSessionIdAsync(Guid sessionId);
    Task<string?> GetAccessTokenAsync();
    Task<string?> GetRefreshTokenAsync();
    Task<DateTime?> GetAccessTokenExpiryAsync();
    Task<Guid?> GetSessionIdAsync();
    Task ClearAsync();
}
