using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class OtpVerifyPage : ContentPage
{
    private readonly IAuthenticationService _auth;

    public OtpVerifyPage(IAuthenticationService auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    private async void OnVerifyClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryOtp.Text) || EntryOtp.Text.Length != 6)
        {
            LblError.Text = "Please enter the 6-digit code.";
            ErrorBanner.IsVisible = true;
            return;
        }

        Loader.IsRunning = Loader.IsVisible = true;
        ErrorBanner.IsVisible = false;

        var (success, error) = await _auth.VerifyOtpAsync(EntryOtp.Text.Trim());

        Loader.IsRunning = Loader.IsVisible = false;

        if (success)
            await Shell.Current.GoToAsync(AppRoutes.Main);
        else
        {
            LblError.Text = error ?? "Invalid code. Please try again.";
            ErrorBanner.IsVisible = true;
        }
    }

    private void OnResendTapped(object? sender, TappedEventArgs e)
        => DisplayAlert("Code Resent", "A new verification code has been sent to your email.", "OK");
}
