using Microsoft.Extensions.DependencyInjection;
using AveroNova.App.UI.Pages;

namespace AveroNova.App.UI
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        private readonly AppShell _appShell;

        public App(AppShell appShell)
        {
            InitializeComponent();
            _appShell = appShell;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(_appShell)
            {
                Title = "AveroNova"
            };

            // Desktop window sizing is Windows-only. Setting Width/Height on Android
            // crashes the process (Fatal signal 11 / SIGSEGV) before the first page.
#if WINDOWS
            window.Width = 1400;
            window.Height = 900;
            window.Created += (sender, args) =>
            {
                if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
                {
                    if (nativeWindow.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                    {
                        presenter.Maximize();
                    }
                }
            };
#endif

            return window;
        }
    }
}