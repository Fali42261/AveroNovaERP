using AveroNova.App.UI.Navigation;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class WelcomePage : ContentPage
{
    public WelcomePage() => InitializeComponent();

    private async void OnLoginClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.Login);

    private async void OnRegisterClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.Register);
}
