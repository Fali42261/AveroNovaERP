using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    // ── Fields ────────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool rememberMe = false;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasError = false;

    [ObservableProperty]
    private string emailError = string.Empty;

    [ObservableProperty]
    private bool hasEmailError = false;

    [ObservableProperty]
    private string passwordError = string.Empty;

    [ObservableProperty]
    private bool hasPasswordError = false;

    // ── Validation ────────────────────────────────────────────────────────────

    public bool Validate()
    {
        HasEmailError    = false;
        HasPasswordError = false;
        HasError         = false;
        EmailError       = string.Empty;
        PasswordError    = string.Empty;
        ErrorMessage     = string.Empty;

        var valid = true;

        if (string.IsNullOrWhiteSpace(Email))
        {
            EmailError    = "Email address is required.";
            HasEmailError = true;
            valid         = false;
        }
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            EmailError    = "Please enter a valid email address.";
            HasEmailError = true;
            valid         = false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            PasswordError    = "Password is required.";
            HasPasswordError = true;
            valid            = false;
        }

        return valid;
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    public void Reset()
    {
        HasError         = false;
        HasEmailError    = false;
        HasPasswordError = false;
        ErrorMessage     = string.Empty;
        EmailError       = string.Empty;
        PasswordError    = string.Empty;
    }
}
