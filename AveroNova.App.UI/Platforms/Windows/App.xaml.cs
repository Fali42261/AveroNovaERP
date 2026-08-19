using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Local;
using Microsoft.UI.Xaml;

namespace AveroNova.App.UI.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            // Must be set in the constructor, before the window exists.
            RequestedTheme = ThemePreferenceStore.Load() == ThemeMode.Dark
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;
            this.InitializeComponent();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
