using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class ForgotPasswordPage : ContentPage
{
    private readonly IAuthenticationService _auth;
    private readonly IToastService _toasts;

    public ForgotPasswordPage(IAuthenticationService auth, IToastService toasts)
    {
        InitializeComponent();
        _auth = auth;
        _toasts = toasts;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _toasts.AttachTo(this);
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        var emailText = EntryEmail.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(emailText))
        {
            LblError.Text = "Please enter your email address.";
            ErrorBanner.IsVisible = true;
            return;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(emailText, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            LblError.Text = "Please enter a valid email address.";
            ErrorBanner.IsVisible = true;
            return;
        }

        Loader.IsRunning = Loader.IsVisible = true;
        ErrorBanner.IsVisible = SuccessBanner.IsVisible = false;

        try
        {
            var (success, error) = await _auth.ForgotPasswordAsync(emailText);

            Loader.IsRunning = Loader.IsVisible = false;

            if (success)
            {
                SuccessBanner.IsVisible = true;
                LblSuccess.Text = "If an account exists with this email, you can reset your password.";
                await Task.Delay(2000);
                await Shell.Current.GoToAsync(AppRoutes.ResetPassword);
            }
            else
            {
                LblError.Text = error ?? "Something went wrong.";
                ErrorBanner.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            Loader.IsRunning = Loader.IsVisible = false;
            LblError.Text = "Something went wrong. Please try again.";
            ErrorBanner.IsVisible = true;
            System.Diagnostics.Debug.WriteLine($"[AveroNova] ForgotPassword failed: {ex}");
        }
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
