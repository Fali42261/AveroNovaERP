using AveroNova.Shared.Security;

namespace AveroNova.App.UI.Services.Security;

public sealed class MauiPendingRegistrationSecretStore : IPendingRegistrationSecretStore
{
    private static string Key(Guid userId)
        => $"{OfflineSessionDefaults.SecurePendingRegistrationPasswordPrefix}{userId:D}";

    public Task SetPendingPasswordAsync(Guid registrationUserId, string password)
        => SecureStorage.Default.SetAsync(Key(registrationUserId), password);

    public Task<string?> GetPendingPasswordAsync(Guid registrationUserId)
        => SecureStorage.Default.GetAsync(Key(registrationUserId));

    public Task ClearPendingPasswordAsync(Guid registrationUserId)
    {
        SecureStorage.Default.Remove(Key(registrationUserId));
        return Task.CompletedTask;
    }
}
