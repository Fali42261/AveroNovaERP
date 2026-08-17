namespace AveroNova.App.UI.Services.Interfaces;

/// <summary>
/// Stable device identifier for license binding. Must not use IMEI or phone number.
/// </summary>
public interface IStableDeviceIdProvider
{
    string GetStableDeviceId();
}
