using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

public interface ISettingsService
{
    AppSettings Get();
    void        Save(AppSettings settings);
    void        SetTheme(ThemeMode mode);
    void        SetLanguage(string code);
    void        SetCurrency(string code, string symbol);
}
