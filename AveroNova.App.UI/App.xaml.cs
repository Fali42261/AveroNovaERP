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
            // Do not set Width/Height on Android: MAUI mutates the native window
            // and crashes with Fatal signal 11 (SIGSEGV). See dotnet/maui#20344.
            var window = new Window(_appShell);

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