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
        _auth = auth;
        _db = db;
        _connectivity = connectivity;
        _sync = sync;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(800);

        try
        {
            await _db.EnsureInitializedAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Local database init failed: {ex}");
        }

        // Local session first. Do not send the user to Login because the API is offline.
        if (await _auth.TryAutoLoginAsync())
        {
            await Shell.Current.GoToAsync(AppRoutes.Main);
            if (_connectivity.IsOnline)
                _ = SafeSyncAsync();
            return;
        }

        if (await _auth.HasLocalUserAsync())
        {
            await Shell.Current.GoToAsync(AppRoutes.Login);
            return;
        }

        await Shell.Current.GoToAsync(AppRoutes.Welcome);
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
