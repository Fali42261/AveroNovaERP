using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Local;

namespace AveroNova.App.UI.Pages.Splash;

public partial class SplashPage : ContentPage
{
    private readonly IAuthenticationService _auth;
    private readonly LocalDatabaseInitializer _db;
    private readonly IConnectivityService _connectivity;
    private readonly ISyncService _sync;

    public SplashPage(
        IAuthenticationService auth,
        LocalDatabaseInitializer db,
        IConnectivityService connectivity,
        ISyncService sync)
    {
        InitializeComponent();
        AveroNova.App.UI.Helpers.StartupLog.Write("Splash ctor");
        _auth = auth;
        _db = db;
        _connectivity = connectivity;
        _sync = sync;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        AveroNova.App.UI.Helpers.StartupLog.Write("Splash OnAppearing");
        await Task.Delay(800);
        AveroNova.App.UI.Helpers.StartupLog.Write("Splash delay done");

        string nextRoute;
        try
        {
            await Task.Run(() => _db.EnsureInitializedAsync());
            AveroNova.App.UI.Helpers.StartupLog.Write("DB initialized");

            if (await _auth.TryAutoLoginAsync())
            {
                AveroNova.App.UI.Helpers.StartupLog.Write("Auto-login OK, going Main");
                nextRoute = AppRoutes.Main;
            }
            else if (await _auth.HasLocalUserAsync())
            {
                nextRoute = AppRoutes.Login;
            }
            else
            {
                nextRoute = AppRoutes.Welcome;
            }
        }
        catch (Exception ex)
        {
            AveroNova.App.UI.Helpers.StartupLog.Write("DB init failed: " + ex);
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Local database init failed: {ex}");
            nextRoute = AppRoutes.Welcome;
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Shell.Current.GoToAsync(nextRoute);
            if (nextRoute == AppRoutes.Main && _connectivity.IsOnline)
                _ = SafeSyncAsync();
        });
    }

    private async Task SafeSyncAsync()
    {
        try
        {
            await _sync.SyncNowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Startup sync skipped: {ex.Message}");
        }
    }
}
