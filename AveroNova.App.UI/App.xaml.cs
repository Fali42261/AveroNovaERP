using AveroNova.App.UI.Services;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        private readonly AppShell _appShell;
        private readonly IConnectivityService _connectivity;
        private readonly ISyncService _sync;
        private readonly IProcurementSyncService _procurementSync;
        private readonly IReturnSyncService _returnSync;

        public App(
            AppShell appShell,
            IConnectivityService connectivity,
            ISyncService sync,
            IBillingService billingService,
            IProcurementSyncService procurementSync,
            IReturnSyncService returnSync)
        {
            InitializeComponent();
            _appShell = appShell;
            _connectivity = connectivity;
            _sync = sync;
            _procurementSync = procurementSync;
            _returnSync = returnSync;
            _ = billingService;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(_appShell)
            {
                Title = "AveroNova"
            };

            window.Created += (_, _) => TriggerSyncIfOnline();
            window.Activated += (_, _) => TriggerSyncIfOnline();

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

        private void TriggerSyncIfOnline()
        {
            if (!_connectivity.IsOnline) return;
            _ = _sync.SyncNowAsync();
            _ = _procurementSync.SyncPendingAsync();
            _ = _returnSync.SyncPendingAsync();
        }
    }
}
