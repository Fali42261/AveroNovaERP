using AveroNova.App.UI.Navigation;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class RegistrationWelcomePage : ContentPage
{
    public RegistrationWelcomePage()
    {
        InitializeComponent();
    }

    private async void OnContinueClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.Register);

    private async void OnLoginClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.Login);
}
