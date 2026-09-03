using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.SubscriptionAccess;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class LoginPage : ContentPage
{
    private readonly IAuthenticationService _auth;
    private readonly IToastService _toasts;
    private bool _layoutBusy;
    private double _appliedMinHeight = double.NaN;
    private ScreenSize? _appliedSize;
    private bool _navBusy;

    public LoginPage(IAuthenticationService auth, IToastService toasts)
    {
        InitializeComponent();
        _auth = auth;
        _toasts = toasts;
        SizeChanged += (_, _) => ApplyLayout();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _toasts.AttachTo(this);
        ApplyLayout();
        HideFieldErrors();
        var pending = PendingAuthMessage.Take();
        if (!string.IsNullOrWhiteSpace(pending))
            ShowBanner(pending);
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
                AuthCard.VerticalOptions = compact ? LayoutOptions.Start : LayoutOptions.Center;

                if (compact)
                {
                    AuthCard.StrokeThickness = 0;
                    AuthCard.BackgroundColor = Colors.Transparent;
                    ContentHost.ClearValue(MinimumHeightRequestProperty);
                    _appliedMinHeight = double.NaN;
                    ContentHost.Padding = new Thickness(20, 24, 20, 160);
                }
                else
                {
                    AuthCard.ClearValue(Border.StrokeThicknessProperty);
                    AuthCard.ClearValue(Border.BackgroundColorProperty);
                }
            }

            if (!compact)
            {
                var minHeight = Height > 0
                    ? Math.Max(0, Height - ContentHost.Padding.VerticalThickness)
                    : -1;
                if (minHeight >= 0
                    && (double.IsNaN(_appliedMinHeight) || Math.Abs(_appliedMinHeight - minHeight) >= 32))
                {
                    _appliedMinHeight = minHeight;
                    ContentHost.MinimumHeightRequest = minHeight;
                }
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
        if (_navBusy)
            return;

        HideFieldErrors();

        var email = (EntryEmail.Text ?? string.Empty).Trim();
        var password = EntryPassword.Text ?? string.Empty;
        var valid = true;

        if (string.IsNullOrWhiteSpace(email))
        {
            ShowFieldError(LblEmailError, "Email address is required");
            valid = false;
        }
        else if (!IsValidEmail(email))
        {
            ShowFieldError(LblEmailError, "Please enter a valid email address.");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowFieldError(LblPasswordError, "Password is required");
            valid = false;
        }

        if (!valid)
            return;

        SetLoading(true);

        try
        {
            var (success, error) = await _auth.LoginAsync(email, password, ChkRemember.IsChecked);

            if (success)
            {
                await Shell.Current.GoToAsync(AppRoutes.Main);
                return;
            }

            ShowCredentialFailure(error);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[swapdigit] Offline login failed: {ex}");
            ShowBanner("Unable to access the local account database. Please try again.");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnRegisterTapped(object? sender, TappedEventArgs e)
        => await NavigateBusyAsync(AppRoutes.Register);

    private async void OnResetPasswordTapped(object? sender, TappedEventArgs e)
        => await NavigateBusyAsync(AppRoutes.ResetPassword);

    private async Task NavigateBusyAsync(string route)
    {
        if (_navBusy)
            return;

        SetLoading(true, "Please wait...");
        try
        {
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[swapdigit] Auth navigation failed route={route}: {ex}");
            ShowBanner("Unable to open this page. Please try again.");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private static bool IsValidEmail(string email)
        => System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

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

    private void ShowCredentialFailure(string? error)
    {
        ErrorBanner.IsVisible = false;
        var isInvalidCredentials = string.IsNullOrWhiteSpace(error)
            || string.Equals(error, AuthenticationMessages.InvalidEmailOrPassword, StringComparison.OrdinalIgnoreCase)
            || string.Equals(error, "Invalid email or password", StringComparison.OrdinalIgnoreCase);

        if (!isInvalidCredentials)
        {
            ShowBanner(error!);
            return;
        }

        _toasts.Show(
            AuthenticationMessages.InvalidEmailOrPassword,
            string.Empty,
            ToastKind.Error,
            TimeSpan.FromSeconds(4));
    }

    private void SetLoading(bool loading, string? busyText = null)
    {
        _navBusy = loading;
        BtnLogin.IsEnabled = !loading;
        BtnLogin.Text = loading ? (busyText ?? "Signing in...") : "Sign In";
        Loader.IsRunning = loading;
        Loader.IsVisible = loading;
        if (loading)
            ErrorBanner.IsVisible = false;
        else
        {
            // Ensure navigation is always enabled after loading completes
            _navBusy = false;
        }
    }
}
