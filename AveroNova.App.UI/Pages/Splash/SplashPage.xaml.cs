using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Splash;

public partial class SplashPage : ContentPage
{
    private readonly IAuthenticationService _auth;
    private readonly IInstallationService _installation;
    private readonly ILicenseService _licenses;

    public SplashPage(IAuthenticationService auth, IInstallationService installation, ILicenseService licenses)
    {
        InitializeComponent();
        _auth = auth;
        _installation = installation;
        _licenses = licenses;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(1200);

        await _installation.EnsureInitializedAsync();

        var licenseStatus = await _licenses.EnsureActivatedAsync();
        if (licenseStatus == LicenseBootstrapStatus.Blocked)
        {
            await Shell.Current.GoToAsync(AppRoutes.LicenseActivation);
            return;
        }

        // Registered + valid local offline session → continue offline (or online).
        if (await _auth.TryAutoLoginAsync())
        {
            await _licenses.ValidateOnlineIfPossibleAsync();
            await Shell.Current.GoToAsync(AppRoutes.Main);
            return;
        }

        // Registered installation → Login only (no Create Account landing).
        if (_installation.IsRegistered)
        {
            await Shell.Current.GoToAsync(AppRoutes.Login);
            return;
        }

        // Fresh installation → Welcome (Login + Create Account).
        await Shell.Current.GoToAsync(AppRoutes.Welcome);
    }
}
