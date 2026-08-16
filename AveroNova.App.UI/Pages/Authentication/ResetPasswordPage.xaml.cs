using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class ResetPasswordPage : ContentPage
{
    private readonly IAuthenticationService _auth;

    public ResetPasswordPage(IAuthenticationService auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    private void OnEmailCompleted(object? sender, EventArgs e) => EntryPassword.Focus();

    private void OnPasswordCompleted(object? sender, EventArgs e) => EntryConfirm.Focus();

    private void OnConfirmCompleted(object? sender, EventArgs e) => OnResetClicked(sender, e);

    private async void OnResetClicked(object? sender, EventArgs e)
    {
        HideFieldErrors();

        var emailMissing = string.IsNullOrWhiteSpace(EntryEmail.Text);
        var passwordMissing = string.IsNullOrWhiteSpace(EntryPassword.Text);
        var confirmMissing = string.IsNullOrWhiteSpace(EntryConfirm.Text);
        var mismatch = !passwordMissing && !confirmMissing
                       && !string.Equals(EntryPassword.Text, EntryConfirm.Text, StringComparison.Ordinal);

        if (emailMissing)
            ShowFieldError(LblEmailError, "Email address is required");
        if (passwordMissing)
            ShowFieldError(LblPasswordError, "New password is required");
        if (confirmMissing)
            ShowFieldError(LblConfirmError, "Confirm password is required");
        else if (mismatch)
            ShowFieldError(LblConfirmError, "Passwords do not match");

        if (emailMissing || passwordMissing || confirmMissing || mismatch)
            return;

        SetLoading(true);

        try
        {
            var (success, error) = await _auth.ResetPasswordAsync(
                EntryEmail.Text.Trim(),
                EntryPassword.Text);

            if (success)
            {
                SuccessBanner.IsVisible = true;
                await Task.Delay(800);
                await Shell.Current.GoToAsync(AppRoutes.Login);
            }
            else
            {
                ShowBanner(error ?? "Unable to reset password. Please try again.");
            }
        }
        catch
        {
            ShowBanner("Unable to reset password. Please try again.");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
        => await GoToLoginAsync();

    private async void OnSignInTapped(object? sender, TappedEventArgs e)
        => await GoToLoginAsync();

    private static Task GoToLoginAsync()
        => Shell.Current.GoToAsync(AppRoutes.Login);

    private static void ShowFieldError(Label label, string message)
    {
        label.Text = message;
        label.IsVisible = true;
    }

    private void HideFieldErrors()
    {
        LblEmailError.IsVisible = false;
        LblPasswordError.IsVisible = false;
        LblConfirmError.IsVisible = false;
        ErrorBanner.IsVisible = false;
        SuccessBanner.IsVisible = false;
    }

    private void ShowBanner(string message)
    {
        LblError.Text = message;
        ErrorBanner.IsVisible = true;
    }

    private void SetLoading(bool loading)
    {
        BtnReset.IsEnabled = !loading;
        BtnReset.Text = loading ? "Resetting..." : "Reset Password";
        Loader.IsRunning = loading;
        Loader.IsVisible = loading;
        if (loading)
            ErrorBanner.IsVisible = false;
    }
}
