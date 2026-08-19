using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Pages;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        private readonly AppShell _appShell;
        private readonly ISettingsService _settings;

        static App()
        {
            AveroNova.App.UI.Helpers.StartupLog.Write("App type loaded");
        }

        public App(AppShell appShell, ISettingsService settings)
        {
            AveroNova.App.UI.Helpers.StartupLog.Write("App ctor start");
            try
            {
                InitializeComponent();
                AveroNova.App.UI.Helpers.StartupLog.Write("App InitializeComponent done");
            }
            catch (Exception ex)
            {
                AveroNova.App.UI.Helpers.StartupLog.Write("App InitializeComponent failed: " + ex);
                throw;
            }
            _appShell = appShell;
            _settings = settings;
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                WriteCrashLog("UnhandledException", args.ExceptionObject);
            TaskScheduler.UnobservedTaskException += (_, args) =>
                WriteCrashLog("UnobservedTaskException", args.Exception);
            AppDomain.CurrentDomain.FirstChanceException += (_, args) =>
            {
                if (args.Exception is Microsoft.Maui.Controls.Xaml.XamlParseException
                    or System.Runtime.InteropServices.COMException)
                    WriteCrashLog("FirstChance", args.Exception);
            };
            _settings.ApplyCurrentTheme();
            RequestedThemeChanged += (_, _) => _settings.ApplyCurrentTheme();
        }

        private static void WriteCrashLog(string kind, object? exception)
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AveroNova.crash.log");
                File.AppendAllText(path, $"{DateTime.Now:O} {kind}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
                // ignore logging failures
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Do not set Width/Height on Android: MAUI mutates the native window
            // and crashes with Fatal signal 11 (SIGSEGV). See dotnet/maui#20344.
            var window = new Window(_appShell);
            AveroNova.App.UI.Helpers.StartupLog.Write("Window created");

#if WINDOWS
            window.Width = 1400;
            window.Height = 900;
#endif

            return window;
        }
    }
}
