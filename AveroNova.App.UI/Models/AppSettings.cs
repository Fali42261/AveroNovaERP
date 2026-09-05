namespace AveroNova.App.UI.Models;

/// <summary>
/// App-level user preferences stored locally on the device.
/// These are not synced to the server — they are per-device settings.
/// </summary>
public class AppSettings
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public ThemeMode  Theme          { get; set; } = ThemeMode.System;
    public string     AccentColor    { get; set; } = "#2563EB";
    public bool       CompactMode    { get; set; }
    public string     Language       { get; set; } = "en";
    public string     DateFormat     { get; set; } = "dd MMM yyyy";
    public string     Currency       { get; set; } = "USD";
    public string     CurrencySymbol { get; set; } = "$";
    public string     TimeZone       { get; set; } = "UTC";
    public bool       Notifications  { get; set; } = true;
    public bool       AutoSync       { get; set; } = true;
    public bool       OfflineMode    { get; set; }
    public bool       RememberLogin  { get; set; }
    public Guid?      LastCompanyId  { get; set; }
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Local;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
