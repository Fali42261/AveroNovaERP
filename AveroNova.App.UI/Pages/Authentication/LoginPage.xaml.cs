using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class LoginPage : ContentPage
{
    private readonly IAuthenticationService _auth;
    private bool _passwordVisible;

    public LoginPage(IAuthenticationService auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryEmail.Text) || string.IsNullOrWhiteSpace(EntryPassword.Text))
        {
            ShowError("Please enter your email and password.");
            return;
        }

        SetLoading(true);

        var (success, error) = await _auth.LoginAsync(
            EntryEmail.Text.Trim(),
            EntryPassword.Text,
            ChkRemember.IsChecked);

        SetLoading(false);

        if (success)
        {
            await Shell.Current.GoToAsync(AppRoutes.Main);
        }
        else
        {
            ShowError(error ?? "Invalid credentials. Please try again.");
        }
    }

    private async void OnForgotPasswordTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.ForgotPassword);

    private async void OnRegisterTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.Register);

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        _passwordVisible        = !_passwordVisible;
        EntryPassword.IsPassword = !_passwordVisible;
    }

    private void ShowError(string message)
    {
        LblError.Text          = message;
        ErrorBanner.IsVisible  = true;
    }

    private void SetLoading(bool loading)
    {
        BtnLogin.IsEnabled   = !loading;
        Loader.IsRunning     = loading;
        Loader.IsVisible     = loading;
        ErrorBanner.IsVisible = false;
    }
}
