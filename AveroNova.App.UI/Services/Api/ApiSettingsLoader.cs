using System.Text.Json;

namespace AveroNova.App.UI.Services.Api;

/// <summary>
/// Loads ApiSettings from the environment-specific MAUI JSON package file and resolves
/// the single BaseUrl for Environment + Platform. This is the only source of truth for API URLs.
/// </summary>
public static class ApiSettingsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ApiSettings Load()
    {
        var fileName = ResolveConfigFileName();
        var settings = ReadSettings(fileName)
                       ?? throw new InvalidOperationException(
                           $"Missing or invalid '{fileName}'. API Base URLs must be configured in central JSON.");

        settings.BaseUrl = ResolveBaseUrl(settings);
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
            throw new InvalidOperationException(
                $"ApiSettings did not resolve a BaseUrl from '{fileName}' for this environment/platform.");

        settings.BaseUrl = EnsureTrailingSlash(settings.BaseUrl);
        if (settings.TimeoutSeconds < 5)
            settings.TimeoutSeconds = 30;

        return settings;
    }

    public static string ResolveConfigFileName()
    {
#if DEBUG
        return "appsettings.Development.json";
#else
        return "appsettings.Production.json";
#endif
    }

    public static string ResolveBaseUrl(ApiSettings settings)
    {
#if DEBUG
        return ResolveDevelopmentBaseUrl(settings);
#else
        return EnsureTrailingSlash(
            string.IsNullOrWhiteSpace(settings.ProductionBaseUrl)
                ? settings.BaseUrl
                : settings.ProductionBaseUrl);
#endif
    }

    public static string ResolveDevelopmentBaseUrl(ApiSettings settings)
    {
#if ANDROID
        if (DeviceInfo.Current.DeviceType == DeviceType.Physical)
            return EnsureTrailingSlash(settings.AndroidDeviceBaseUrl);
        return EnsureTrailingSlash(settings.AndroidEmulatorBaseUrl);
#elif IOS || MACCATALYST
        var ios = string.IsNullOrWhiteSpace(settings.IosSimulatorBaseUrl)
            ? settings.WindowsBaseUrl
            : settings.IosSimulatorBaseUrl;
        return EnsureTrailingSlash(ios);
#else
        return EnsureTrailingSlash(settings.WindowsBaseUrl);
#endif
    }

    private static ApiSettings? ReadSettings(string fileName)
    {
        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync(fileName).GetAwaiter().GetResult();
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty(ApiSettings.SectionName, out var section))
                return null;

            var settings = JsonSerializer.Deserialize<ApiSettings>(section.GetRawText(), JsonOptions)
                           ?? new ApiSettings();

            // Production JSON uses "BaseUrl"; map into ProductionBaseUrl for clarity.
            if (section.TryGetProperty("BaseUrl", out var prodBase)
                && prodBase.GetString() is { Length: > 0 } url
                && string.IsNullOrWhiteSpace(settings.ProductionBaseUrl))
            {
                settings.ProductionBaseUrl = url;
            }

            if (section.TryGetProperty("TimeoutSeconds", out var timeout)
                && timeout.TryGetInt32(out var seconds))
            {
                settings.TimeoutSeconds = seconds;
            }

            return settings;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string EnsureTrailingSlash(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;
        url = url.Trim();
        return url.EndsWith('/') ? url : url + "/";
    }
}
