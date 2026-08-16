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
                Width = 1400,
                Height = 900
            };

#if WINDOWS
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