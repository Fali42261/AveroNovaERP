using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        private readonly AppShell _appShell;
        private readonly IResponsiveLayoutService _layout;

        public App(AppShell appShell, IResponsiveLayoutService layout)
        {
            InitializeComponent();
            _appShell = appShell;
            _layout = layout;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(_appShell)
            {
                Title = "AveroNova ERP",
                Width = 1280,
                Height = 800,
                MinimumWidth = 360,
                MinimumHeight = 520
            };

            window.SizeChanged += (_, _) => _layout.Update(window.Width, window.Height);
            window.HandlerChanged += (_, _) => _layout.Update(window.Width, window.Height);

            return window;
        }
    }
}
