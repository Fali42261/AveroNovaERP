using Microsoft.Extensions.DependencyInjection;
using AveroNova.App.UI.Pages;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        private readonly AppShell _appShell;
        private readonly ISessionInactivityService _inactivity;

        public App(AppShell appShell, ISessionInactivityService inactivity)
        {
            InitializeComponent();
            _appShell = appShell;
            _inactivity = inactivity;
            _inactivity.Start();
            _appShell.Navigated += (_, _) =>
            {
                _inactivity.RecordActivity();
                if (_appShell.CurrentPage is ContentPage page && page.Content is View content)
                    _inactivity.Track(content);
            };
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(_appShell)
            {
                Title = "SwipeDigit"
            };
            window.Activated += async (_, _) =>
            {
                await _inactivity.CheckNowAsync();
                _inactivity.RecordActivity();
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
