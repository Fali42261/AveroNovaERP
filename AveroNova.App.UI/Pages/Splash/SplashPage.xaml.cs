using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Splash;

public partial class SplashPage : ContentPage
{
    private readonly IAuthenticationService _auth;

    public SplashPage(IAuthenticationService auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(1200); // Show splash briefly

        bool autoLogin = await _auth.TryAutoLoginAsync();
        await Shell.Current.GoToAsync(autoLogin ? AppRoutes.Main : AppRoutes.Welcome);
    }
}
