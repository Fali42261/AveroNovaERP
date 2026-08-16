using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class LoginPage : ContentPage
{
    private readonly IAuthenticationService _auth;
    private bool _layoutBusy;
    private double _appliedMinHeight = double.NaN;
    private ScreenSize? _appliedSize;

    public LoginPage(IAuthenticationService auth)
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

    private void OnPasswordCompleted(object? sender, EventArgs e) => OnLoginClicked(sender, e);

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        HideFieldErrors();

        var emailMissing = string.IsNullOrWhiteSpace(EntryEmail.Text);
        var passwordMissing = string.IsNullOrWhiteSpace(EntryPassword.Text);

        if (emailMissing)
            ShowFieldError(LblEmailError, "Email address is required");
        if (passwordMissing)
            ShowFieldError(LblPasswordError, "Password is required");
        if (emailMissing || passwordMissing)
            return;

        SetLoading(true);

        try
        {
            var (success, error) = await _auth.LoginAsync(
                EntryEmail.Text.Trim(),
                EntryPassword.Text,
                ChkRemember.IsChecked);

            if (success)
            {
                await Shell.Current.GoToAsync(AppRoutes.Main);
            }
            else
            {
                ShowBanner(error ?? "Invalid credentials. Please try again.");
            }
        }
        catch
        {
            ShowBanner("Unable to sign in. Please try again.");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnRegisterTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.Register);

    private async void OnResetPasswordTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.ResetPassword);

    private static void ShowFieldError(Label label, string message)
    {
        label.Text = message;
        label.IsVisible = true;
    }

    private void HideFieldErrors()
    {
        LblEmailError.IsVisible = false;
        LblPasswordError.IsVisible = false;
        ErrorBanner.IsVisible = false;
    }

    private void ShowBanner(string message)
    {
        LblError.Text = message;
        ErrorBanner.IsVisible = true;
    }

    private void SetLoading(bool loading)
    {
        BtnLogin.IsEnabled = !loading;
        BtnLogin.Text = loading ? "Signing in..." : "Sign In";
        Loader.IsRunning = loading;
        Loader.IsVisible = loading;
        if (loading)
            ErrorBanner.IsVisible = false;
    }
}
