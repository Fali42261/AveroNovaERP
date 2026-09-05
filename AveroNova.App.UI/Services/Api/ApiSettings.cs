namespace AveroNova.App.UI.Services.Api;

/// <summary>
/// Central API Base URL settings loaded from MAUI appsettings.*.json.
/// Absolute URLs live only in JSON — services use relative paths via <see cref="IApiClient"/>.
/// </summary>
public sealed class ApiSettings
{
    public const string SectionName = "ApiSettings";

    /// <summary>Resolved Base URL for the current environment + platform (always ends with /).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Development — Windows MAUI / host browser.</summary>
    public string WindowsBaseUrl { get; set; } = string.Empty;

    /// <summary>Development — physical Android device on the same LAN as the Windows host.</summary>
    public string AndroidDeviceBaseUrl { get; set; } = string.Empty;

    /// <summary>Development — Android emulator host loopback mapping.</summary>
    public string AndroidEmulatorBaseUrl { get; set; } = string.Empty;

    /// <summary>Development — iOS Simulator (optional).</summary>
    public string IosSimulatorBaseUrl { get; set; } = string.Empty;

    /// <summary>Production — single public API Base URL (configuration only; no Azure deploy yet).</summary>
    public string ProductionBaseUrl { get; set; } = string.Empty;
}
