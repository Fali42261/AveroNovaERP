using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Local;

public sealed class LocalSettingsService : ISettingsService
{
    private AppSettings _settings;

    public LocalSettingsService()
    {
        _settings = new AppSettings
        {
            Theme = ThemePreferenceStore.Load()
        };
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
        _settings.Currency = code;
        _settings.CurrencySymbol = symbol;
    }
}