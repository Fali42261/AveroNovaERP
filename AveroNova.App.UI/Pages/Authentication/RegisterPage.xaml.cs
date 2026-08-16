using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class RegisterPage : ContentPage
{
    private readonly IAuthenticationService _auth;
    private RegisterViewModel _vm = null!;

    private readonly Color _focusBorderColor;
    private readonly Color _errorBorderColor;

    public RegisterPage(IAuthenticationService auth)
    {
        InitializeComponent();
        _auth = auth;

        _vm = (RegisterViewModel)BindingContext;

        _focusBorderColor = ResolveColor("PrimaryColor", "InputFocusBorder", "#2563EB");
        _errorBorderColor = ResolveColor("ErrorColor", "ErrorBorder", "#EF4444");
    }

    private static Color ResolveColor(params string[] tryKeysAndFallback)
    {
        foreach (var key in tryKeysAndFallback)
        {
            if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var obj) == true && obj is Color c)
                return c;
        }
        var fallback = tryKeysAndFallback[^1];
        try { return Color.FromArgb(fallback); } catch { return Colors.Gray; }
    }

    private Color GetDefaultBorderColor()
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        bool isDark = app?.RequestedTheme == AppTheme.Dark;

        string key = isDark ? "BorderColorDark" : "InputBorder";
        if (app?.Resources.TryGetValue(key, out var obj) == true && obj is Color c)
            return c;

        return isDark ? Color.FromArgb("#334155") : Color.FromArgb("#D1D5DB");
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        UpdateEyeIcons();

        WireFocusState(EntryFirstName, FirstNameBorder, () => _vm.HasFirstNameError);
        WireFocusState(EntryLastName, LastNameBorder, () => _vm.HasLastNameError);
        WireFocusState(EntryEmail, EmailBorder, () => _vm.HasEmailError);
        WireFocusState(EntryMobile, MobileBorder, () => _vm.HasMobileError);
        WireFocusState(EntryPassword, PasswordBorder, () => _vm.HasPasswordError);
        WireFocusState(EntryConfirmPassword, ConfirmPasswordBorder, () => _vm.HasConfirmPasswordError);

        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RegisterViewModel.HasFirstNameError))
                ApplyErrorBorder(FirstNameBorder, _vm.HasFirstNameError, EntryFirstName.IsFocused);
            else if (e.PropertyName == nameof(RegisterViewModel.HasLastNameError))
                ApplyErrorBorder(LastNameBorder, _vm.HasLastNameError, EntryLastName.IsFocused);
            else if (e.PropertyName == nameof(RegisterViewModel.HasEmailError))
                ApplyErrorBorder(EmailBorder, _vm.HasEmailError, EntryEmail.IsFocused);
            else if (e.PropertyName == nameof(RegisterViewModel.HasMobileError))
                ApplyErrorBorder(MobileBorder, _vm.HasMobileError, EntryMobile.IsFocused);
            else if (e.PropertyName == nameof(RegisterViewModel.HasPasswordError))
                ApplyErrorBorder(PasswordBorder, _vm.HasPasswordError, EntryPassword.IsFocused);
            else if (e.PropertyName == nameof(RegisterViewModel.HasConfirmPasswordError))
                ApplyErrorBorder(ConfirmPasswordBorder, _vm.HasConfirmPasswordError, EntryConfirmPassword.IsFocused);
        };
    }

    private void WireFocusState(Entry entry, Border border, Func<bool> hasError)
    {
        entry.Focused += (s, e) =>
        {
            if (hasError())
                border.Stroke = _errorBorderColor;
            else
                border.Stroke = _focusBorderColor;
        };
        entry.Unfocused += (s, e) =>
        {
            if (hasError())
                border.Stroke = _errorBorderColor;
            else
                border.Stroke = GetDefaultBorderColor();
        };
    }

    private void ApplyErrorBorder(Border border, bool hasError, bool isFocused)
    {
        if (hasError)
            border.Stroke = _errorBorderColor;
        else if (isFocused)
            border.Stroke = _focusBorderColor;
        else
            border.Stroke = GetDefaultBorderColor();
    }

    private static string GetIconString(string key)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var obj) == true && obj is string s)
            return s;
        return string.Empty;
    }

    private void UpdateEyeIcons()
    {
        string iconEye = GetIconString("IconEye");
        string iconEyeOff = GetIconString("IconEyeOff");

        if (string.IsNullOrEmpty(iconEye)) iconEye = "\u25CE";
        if (string.IsNullOrEmpty(iconEyeOff)) iconEyeOff = "\u2299";

        BtnTogglePassword.Text = _vm.IsPasswordHidden ? iconEye : iconEyeOff;
        BtnToggleConfirmPassword.Text = _vm.IsConfirmPasswordHidden ? iconEye : iconEyeOff;
    }

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        _vm.TogglePasswordVisibilityCommand.Execute(null);
        UpdateEyeIcons();
    }

    private void OnToggleConfirmPasswordClicked(object sender, EventArgs e)
    {
        _vm.ToggleConfirmPasswordVisibilityCommand.Execute(null);
        UpdateEyeIcons();
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        if (!_vm.ValidateForm())
        {
            return;
        }

        _vm.IsBusy = true;
        _vm.HasGeneralError = false;
        _vm.GeneralError = string.Empty;
        _vm.HasGeneralSuccess = false;
        _vm.GeneralSuccess = string.Empty;

        try
        {
            string fullName = $"{_vm.FirstName.Trim()} {_vm.LastName.Trim()}".Trim();

            var (success, error) = await _auth.RegisterAsync(
                fullName,
                _vm.Email.Trim(),
                _vm.Password);

            if (success)
            {
                _vm.HasGeneralSuccess = true;
                _vm.GeneralSuccess = "Account created successfully! Verifying...";
                await Task.Delay(600);
                await Shell.Current.GoToAsync(AppRoutes.OtpVerify);
            }
            else
            {
                _vm.HasGeneralError = true;
                _vm.GeneralError = error ?? "Registration failed. Please try again.";
            }
        }
        catch (Exception ex)
        {
            _vm.HasGeneralError = true;
            _vm.GeneralError = ex.Message;
        }
        finally
        {
            _vm.IsBusy = false;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnLoginTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
