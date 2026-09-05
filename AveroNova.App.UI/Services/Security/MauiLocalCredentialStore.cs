using AveroNova.Shared.Security;

namespace AveroNova.App.UI.Services.Security;

public sealed class MauiLocalCredentialStore : ILocalCredentialStore
{
    public async Task SetPasswordHashAsync(Guid userId, string email, string passwordHash)
    {
        await SecureStorage.Default.SetAsync(HashKey(userId), passwordHash);
        var normalized = NormalizeEmail(email);
        if (!string.IsNullOrWhiteSpace(normalized))
            await SecureStorage.Default.SetAsync(EmailKey(normalized), userId.ToString("D"));
    }

    public Task<string?> GetPasswordHashAsync(Guid userId)
        => SecureStorage.Default.GetAsync(HashKey(userId));

    public async Task<Guid?> FindUserIdByEmailAsync(string email)
    {
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var raw = await SecureStorage.Default.GetAsync(EmailKey(normalized));
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static string HashKey(Guid userId)
        => $"{OfflineSessionDefaults.SecureCredentialHashPrefix}{userId:D}";

    private static string EmailKey(string email)
        => $"{OfflineSessionDefaults.SecureCredentialEmailPrefix}{email}";

    private static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();
}
