using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Local;

namespace AveroNova.App.UI.Services.Mock;

/// <summary>
/// Device-local user preferences. Theme is persisted so it survives
/// restart, login, and logout.
/// </summary>
public class MockSettingsService : ISettingsService
{
    private AppSettings _settings = new();

    public MockSettingsService()
    {
        _settings.Theme = ThemePreferenceStore.Load();
    }

    public AppSettings Get() => _settings;

    public void Save(AppSettings settings)
    {
        _settings = settings;
        if (_settings.Theme == ThemeMode.System)
            _settings.Theme = ThemeMode.Light;
        ThemePreferenceStore.Save(_settings.Theme);
    }

    public void SetTheme(ThemeMode mode)
    {
        if (mode == ThemeMode.System)
            mode = ThemeMode.Light;

        _settings.Theme = mode;
        ThemePreferenceStore.Save(mode);
        ApplyCurrentTheme();
    }

    public void ApplyCurrentTheme()
        => AppThemeSync.Apply(ThemePreferenceStore.ToAppTheme(_settings.Theme));

    public void SetLanguage(string code) => _settings.Language = code;

    public void SetCurrency(string code, string symbol)
    {
        _settings.Currency       = code;
        _settings.CurrencySymbol = symbol;
    }
}
