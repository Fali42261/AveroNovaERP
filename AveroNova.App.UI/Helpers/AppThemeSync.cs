namespace AveroNova.App.UI.Helpers;

/// <summary>
/// Applies MAUI UserAppTheme and, on Windows, the window ElementTheme so native
/// controls, ContentDialogs, and toast-style alerts follow Dark/Light.
/// Do not call SyncWindows during window construction — wait until Created/HandlerChanged.
/// </summary>
public static class AppThemeSync
{
    public static void Apply(AppTheme theme)
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app is not null)
            app.UserAppTheme = theme;

        SyncWindows(theme);
    }

    public static void SyncFromCurrentApp()
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app is null)
            return;

        SyncWindows(app.UserAppTheme);
    }

    private static void SyncWindows(AppTheme theme)
    {
#if WINDOWS
        var elementTheme = theme == AppTheme.Dark
            ? Microsoft.UI.Xaml.ElementTheme.Dark
            : Microsoft.UI.Xaml.ElementTheme.Light;

        var windows = Microsoft.Maui.Controls.Application.Current?.Windows;
        if (windows is null)
            return;

        foreach (var window in windows)
        {
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native
                && native.Content is Microsoft.UI.Xaml.FrameworkElement content
                && content.RequestedTheme != elementTheme)
            {
                content.RequestedTheme = elementTheme;
            }
        }
#endif
    }
}
