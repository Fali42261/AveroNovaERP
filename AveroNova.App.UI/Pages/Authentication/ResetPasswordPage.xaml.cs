using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class ResetPasswordPage : ContentPage
{
    private readonly IAuthenticationService _auth;
    private readonly IToastService _toasts;
    private readonly HashSet<string> _touched = [];
    private bool _layoutBusy;
    private bool _busy;
    private bool _interactionReady;
    private bool _unfocusedAttached;
    private double _appliedMinHeight = double.NaN;
    private ScreenSize? _appliedSize;

    public ResetPasswordPage(IAuthenticationService auth, IToastService toasts)
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
        _interactionReady = false;
        AttachFieldValidation();
        ApplyLayout();
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(400), () => _interactionReady = true);
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

    private void AttachFieldValidation()
    {
        if (_unfocusedAttached)
            return;

        _unfocusedAttached = true;
        AttachField(EntryEmail, "Email");
        AttachField(EntryPassword, "Password");
        AttachField(EntryConfirm, "Confirm");
    }

    private void AttachField(Entry entry, string field)
    {
        entry.Focused += (_, _) =>
        {
            if (_interactionReady)
                _touched.Add(field);
        };
        entry.Unfocused += (_, _) =>
        {
            if (_interactionReady && _touched.Contains(field))
                ValidateField(field);
        };
    }

    private void OnEmailCompleted(object? sender, EventArgs e)
    {
        _touched.Add("Email");
        ValidateField("Email");
        EntryPassword.Focus();
    }

    private void OnPasswordCompleted(object? sender, EventArgs e)
    {
        _touched.Add("Password");
        ValidateField("Password");
        EntryConfirm.Focus();
    }

    private void OnConfirmCompleted(object? sender, EventArgs e) => OnResetClicked(sender, e);

    private async void OnResetClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;

        _touched.Add("Email");
        _touched.Add("Password");
        _touched.Add("Confirm");
        ErrorBanner.IsVisible = false;

        if (!ValidateAll())
            return;

        SetLoading(true);

        try
        {
            var (success, error) = await _auth.ResetPasswordAsync(
                (EntryEmail.Text ?? string.Empty).Trim(),
                EntryPassword.Text ?? string.Empty);

            if (success)
            {
                _toasts.ShowSuccess(
                    "Password Updated",
                    "Your local password was changed successfully. Sign in with the new password.",
                    TimeSpan.FromSeconds(2));
                await Task.Delay(500);
                await GoToLoginAsync();
            }
            else
            {
                ShowBanner(error ?? "Unable to reset password. Please try again.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[swapdigit] Password reset failed: {ex}");
            ShowBanner("Unable to access the local account database. Please try again.");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnSignInTapped(object? sender, TappedEventArgs e)
    {
        if (_busy)
            return;

        SetLoading(true, "Please wait...");
        try
        {
            await GoToLoginAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[swapdigit] Navigate to login failed: {ex}");
            ShowBanner("Unable to open Sign In. Please try again.");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private static Task GoToLoginAsync()
        => Shell.Current.GoToAsync(AppRoutes.Login);

    private bool ValidateAll()
    {
        var emailOk = ValidateField("Email");
        var passwordOk = ValidateField("Password");
        var confirmOk = ValidateField("Confirm");
        return emailOk && passwordOk && confirmOk;
    }

    private bool ValidateField(string field)
    {
        switch (field)
        {
            case "Email":
            {
                var email = (EntryEmail.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(email))
                {
                    ShowFieldError(LblEmailError, "Email address is required");
                    return false;
                }

                if (!IsValidEmail(email))
                {
                    ShowFieldError(LblEmailError, "Please enter a valid email address.");
                    return false;
                }

                ShowFieldError(LblEmailError, null);
                return true;
            }
            case "Password":
            {
                var password = EntryPassword.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(password))
                {
                    ShowFieldError(LblPasswordError, "New password is required");
                    return false;
                }

                if (password.Length < 8)
                {
                    ShowFieldError(LblPasswordError, "Password must be at least 8 characters");
                    return false;
                }

                ShowFieldError(LblPasswordError, null);
                if (_touched.Contains("Confirm") && !string.IsNullOrWhiteSpace(EntryConfirm.Text))
                    ValidateField("Confirm");
                return true;
            }
            case "Confirm":
            {
                var confirm = EntryConfirm.Text ?? string.Empty;
                var password = EntryPassword.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(confirm))
                {
                    ShowFieldError(LblConfirmError, "Confirm password is required");
                    return false;
                }

                if (!string.Equals(password, confirm, StringComparison.Ordinal))
                {
                    ShowFieldError(LblConfirmError, "Passwords do not match");
                    return false;
                }

                ShowFieldError(LblConfirmError, null);
                return true;
            }
            default:
                return true;
        }
    }

    private static bool IsValidEmail(string email)
        => System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    private static void ShowFieldError(Label label, string? message)
    {
        label.Text = message ?? string.Empty;
        label.IsVisible = !string.IsNullOrEmpty(message);
    }

    private void ShowBanner(string message)
    {
        LblError.Text = message;
        ErrorBanner.IsVisible = true;
    }

    private void SetLoading(bool loading, string? busyText = null)
    {
        _busy = loading;
        BtnReset.IsEnabled = !loading;
        BtnReset.Text = loading ? (busyText ?? "Resetting...") : "Reset Password";
        Loader.IsRunning = loading;
        Loader.IsVisible = loading;
        if (loading)
            ErrorBanner.IsVisible = false;
    }
}
