using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class LoginPage : ContentPage
{
    private readonly IAuthenticationService _auth;
    private readonly RegistrationWizardViewModel _wizard;
    private bool _passwordVisible;

    public LoginPage(IAuthenticationService auth, RegistrationWizardViewModel wizard)
    {
        InitializeComponent();
        _auth = auth;
        _wizard = wizard;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        ErrorBanner.IsVisible = false;

        if (string.IsNullOrWhiteSpace(EntryEmail.Text) || string.IsNullOrWhiteSpace(EntryPassword.Text))
        {
            ShowError("Please enter your email and password.");
            return;
        }

        await BusyButton.RunAsync(button, async () =>
        {
            if (!NetworkStatus.HasInternet)
            {
                ShowError(UserMessages.InternetRequired);
                return;
            }

            Loader.IsRunning = Loader.IsVisible = true;
            try
            {
                var (success, error) = await _auth.LoginAsync(
                    EntryEmail.Text.Trim(),
                    EntryPassword.Text,
                    ChkRemember.IsChecked);

                if (success)
                {
                    await Shell.Current.GoToAsync(AppRoutes.Main);
                    return;
                }

                ShowError(string.IsNullOrWhiteSpace(error) || error.Contains("Exception")
                    ? UserMessages.InvalidCredentials
                    : error);
            }
            catch
            {
                ShowError(UserMessages.ServerUnavailable);
            }
            finally
            {
                Loader.IsRunning = Loader.IsVisible = false;
            }
        }, "Loading...");
    }

    private async void OnForgotPasswordTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.ForgotPassword);

    private async void OnRegisterTapped(object? sender, TappedEventArgs e)
    {
        _wizard.BeginNewCompanyRegistration();
        await Shell.Current.GoToAsync(AppRoutes.Register);
    }

    private void OnTogglePasswordClicked(object? sender, EventArgs e)
    {
        _passwordVisible = !_passwordVisible;
        EntryPassword.IsPassword = !_passwordVisible;
        BtnTogglePassword.Text = _passwordVisible ? "Hide" : "Show";
    }

    private void ShowError(string message)
    {
        LblError.Text = message;
        ErrorBanner.IsVisible = true;
    }
}
