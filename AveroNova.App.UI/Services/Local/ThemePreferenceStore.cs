using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Local;

/// <summary>
/// Persists Light/Dark on disk so the choice survives process restart.
/// Uses a file under LocalApplicationData because Windows unpackaged
/// Preferences/ApplicationData can fail to keep values after close.
/// </summary>
internal static class ThemePreferenceStore
{
    private const string FileName = "theme.preference";
    private const string PreferencesKey = "averonova.settings.theme";

    public static ThemeMode Load()
    {
        try
        {
            var path = GetFilePath();
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path).Trim();
                if (TryParse(text, out var fromFile))
                    return fromFile;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Theme file load failed: {ex.Message}");
        }

        try
        {
            var stored = Preferences.Default.Get(PreferencesKey, string.Empty);
            if (TryParse(stored, out var fromPreferences))
                return fromPreferences;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Theme preference load failed: {ex.Message}");
        }

        return ThemeMode.Light;
    }

    public static void Save(ThemeMode mode)
    {
        if (mode == ThemeMode.System)
            mode = ThemeMode.Light;

        try
        {
            var path = GetFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, mode.ToString());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Theme file save failed: {ex.Message}");
        }

        try
        {
            Preferences.Default.Set(PreferencesKey, mode.ToString());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Theme preference save failed: {ex.Message}");
        }
    }

    public static AppTheme ToAppTheme(ThemeMode mode) => mode switch
    {
        ThemeMode.Dark => AppTheme.Dark,
        _ => AppTheme.Light
    };

    private static bool TryParse(string? value, out ThemeMode mode)
    {
        if (Enum.TryParse(value, true, out mode) && mode != ThemeMode.System)
            return true;

        mode = ThemeMode.Light;
        return false;
    }

    private static string GetFilePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "AveroNova", FileName);
    }
}
