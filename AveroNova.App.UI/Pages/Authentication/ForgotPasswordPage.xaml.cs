using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class ForgotPasswordPage : ContentPage
{
    private readonly IAuthenticationService _auth;

    public ForgotPasswordPage(IAuthenticationService auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryEmail.Text))
        {
            LblError.Text = "Please enter your email address.";
            ErrorBanner.IsVisible = true;
            return;
        }

        Loader.IsRunning = Loader.IsVisible = true;
        ErrorBanner.IsVisible = SuccessBanner.IsVisible = false;

        var (success, error) = await _auth.ForgotPasswordAsync(EntryEmail.Text.Trim());

        Loader.IsRunning = Loader.IsVisible = false;

        if (success)
            SuccessBanner.IsVisible = true;
        else
        {
            LblError.Text = error ?? "Something went wrong.";
            ErrorBanner.IsVisible = true;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
