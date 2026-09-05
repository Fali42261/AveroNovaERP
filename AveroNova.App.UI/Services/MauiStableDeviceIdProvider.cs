using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services;

/// <summary>
/// Platform-appropriate installation/device identifier (not IMEI / phone number).
/// Android: ANDROID_ID (survives app reinstall). iOS: IdentifierForVendor.
/// Other platforms: persisted app preference, then a generated GUID.
/// </summary>
public sealed class MauiStableDeviceIdProvider : IStableDeviceIdProvider
{
    private const string PreferenceKey = "averonova.stable_device_id";

    public string GetStableDeviceId()
    {
        var existing = Preferences.Default.Get(PreferenceKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(existing))
            return existing;

        var generated = CreatePlatformId();
        Preferences.Default.Set(PreferenceKey, generated);
        return generated;
    }

    private static string CreatePlatformId()
    {
#if ANDROID
        try
        {
            var androidId = Android.Provider.Settings.Secure.GetString(
                Android.App.Application.Context.ContentResolver,
                Android.Provider.Settings.Secure.AndroidId);
            if (!string.IsNullOrWhiteSpace(androidId))
                return "android-" + androidId;
        }
        catch
        {
            // fall through to generated id
        }
#elif IOS || MACCATALYST
        try
        {
            var vendor = UIKit.UIDevice.CurrentDevice.IdentifierForVendor?.AsString();
            if (!string.IsNullOrWhiteSpace(vendor))
                return "ios-" + vendor;
        }
        catch
        {
            // fall through to generated id
        }
#elif WINDOWS
        try
        {
            var systemId = Windows.System.Profile.SystemIdentification.GetSystemIdForPublisher();
            if (systemId?.Id is not null)
            {
                var reader = Windows.Storage.Streams.DataReader.FromBuffer(systemId.Id);
                var bytes = new byte[systemId.Id.Length];
                reader.ReadBytes(bytes);
                return "win-" + Convert.ToHexString(bytes);
            }
        }
        catch
        {
            // fall through to generated id
        }
#endif
        return "app-" + Guid.NewGuid().ToString("N");
    }
}
