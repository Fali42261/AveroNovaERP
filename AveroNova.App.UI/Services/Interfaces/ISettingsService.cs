using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

public interface ISettingsService
{
    Task<AppSettings> GetAsync();
    Task<(bool Ok, string? Error)> SaveAsync(AppSettings settings);
}
