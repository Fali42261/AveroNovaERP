using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockSettingsService : ISettingsService
{
    private AppSettings _settings = new();

    public AppSettings Get() => _settings;

    public void Save(AppSettings settings) => _settings = settings;

    public void SetTheme(ThemeMode mode) => _settings.Theme = mode;

    public void SetLanguage(string code) => _settings.Language = code;

    public void SetCurrency(string code, string symbol)
    {
        _settings.Currency       = code;
        _settings.CurrencySymbol = symbol;
    }
}
