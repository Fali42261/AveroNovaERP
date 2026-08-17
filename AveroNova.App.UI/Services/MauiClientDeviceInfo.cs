using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services;

public sealed class MauiClientDeviceInfo : IClientDeviceInfo
{
    public string Name => DeviceInfo.Name;
    public string Platform => DeviceInfo.Platform.ToString();
}
