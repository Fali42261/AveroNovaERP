using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockSettingsService : ISettingsService
{
    private AppSettings _settings = new();

    public Task<AppSettings> GetAsync() => Task.FromResult(_settings);
    public Task<(bool Ok, string? Error)> SaveAsync(AppSettings settings)
    { _settings = settings; return Task.FromResult<(bool, string?)>((true, null)); }
}
