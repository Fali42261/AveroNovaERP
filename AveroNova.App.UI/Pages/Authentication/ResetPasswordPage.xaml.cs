using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Shared.Security;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class ResetPasswordPage : ContentPage
{
    private readonly IAuthenticationService _auth;
    private bool _layoutBusy;
    private double _appliedMinHeight = double.NaN;
    private ScreenSize? _appliedSize;

    public ResetPasswordPage(IAuthenticationService auth)
    {
        InitializeComponent();
        _auth = auth;
        SizeChanged += (_, _) => ApplyLayout();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (_layoutBusy || Width <= 0)
            return;

        var size = ResponsiveBreakpoints.FromWidth(Width);
        var compact = size == ScreenSize.Compact;
        var sizeChanged = _appliedSize != size;

        _layoutBusy = true;
        try
        {
            if (sizeChanged)
            {
                _appliedSize = size;
                ContentHost.Padding = size switch
                {
                    ScreenSize.Compact => new Thickness(20, 24),
                    ScreenSize.Medium => new Thickness(32, 28),
                    _ => new Thickness(40, 36)
                };

                AuthCard.HorizontalOptions = LayoutOptions.Center;
                AuthCard.Padding = compact ? new Thickness(4, 8) : new Thickness(32);

                if (compact)
                {
                    AuthCard.StrokeThickness = 0;
                    AuthCard.BackgroundColor = Colors.Transparent;
                }
                else
                {
                    AuthCard.ClearValue(Border.StrokeThicknessProperty);
                    AuthCard.ClearValue(Border.BackgroundColorProperty);
                }
            }

            var minHeight = Height > 0
                ? Math.Max(0, Height - ContentHost.Padding.VerticalThickness)
                : -1;
            if (minHeight >= 0
                && (double.IsNaN(_appliedMinHeight) || Math.Abs(_appliedMinHeight - minHeight) >= 32))
            {
                _appliedMinHeight = minHeight;
                ContentHost.MinimumHeightRequest = minHeight;
            }

            var available = Math.Max(280, Width - ContentHost.Padding.HorizontalThickness);
            var cardWidth = Math.Min(available, compact ? 480 : 440);
            if (Math.Abs(AuthCard.WidthRequest - cardWidth) >= 1)
            {
                AuthCard.WidthRequest = cardWidth;
                AuthCard.MaximumWidthRequest = cardWidth;
            }
        }
        finally
        {
            _layoutBusy = false;
        }
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
        else if (!PasswordPolicy.IsStrong(EntryPassword.Text))
            ShowFieldError(LblPasswordError, PasswordPolicy.RequirementMessage);
        if (confirmMissing)
            ShowFieldError(LblConfirmError, "Confirm password is required");
        else if (mismatch)
            ShowFieldError(LblConfirmError, "Passwords do not match");

        if (emailMissing || passwordMissing || confirmMissing || mismatch || !PasswordPolicy.IsStrong(EntryPassword.Text))
            return;

        SetLoading(true);

        try
        {
            var (success, error) = await _auth.ResetPasswordAsync(
                EntryEmail.Text.Trim(),
                EntryPassword.Text);

            if (success)
            {
                await AppToast.ShowAsync(this, "Password reset successfully.", AppToastKind.Success);
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
        ErrorBanner.IsVisible = false;
        _ = AppToast.ShowAsync(this, message, AppToastKind.Error);
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
