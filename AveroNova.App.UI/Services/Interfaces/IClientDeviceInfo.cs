namespace AveroNova.App.UI.Services.Interfaces;

/// <summary>
/// Abstraction over MAUI DeviceInfo so auth can be tested without platform runtime.
/// </summary>
public interface IClientDeviceInfo
{
    string Name { get; }
    string Platform { get; }
}
