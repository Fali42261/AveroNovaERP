using System.Text.Json;
using AveroNova.Shared.Security;

namespace AveroNova.App.UI.Services.Security;

public sealed class MauiLicenseAnchorStore : ILicenseAnchorStore
{
    public Task SaveAsync(LicenseAnchor anchor)
        => SecureStorage.Default.SetAsync(
            OfflineSessionDefaults.SecureLicenseAnchorKey,
            JsonSerializer.Serialize(anchor));

    public async Task<LicenseAnchor?> LoadAsync()
    {
        var raw = await SecureStorage.Default.GetAsync(OfflineSessionDefaults.SecureLicenseAnchorKey);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonSerializer.Deserialize<LicenseAnchor>(raw);
        }
        catch
        {
            return null;
        }
    }
}
