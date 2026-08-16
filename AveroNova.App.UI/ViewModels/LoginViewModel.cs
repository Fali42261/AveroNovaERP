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

    // ── Mock Credentials ──────────────────────────────────────────────────────

    private const string MockEmail    = "admin@averonova.com";
    private const string MockPassword = "Admin@123";

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
            EmailError    = "Email or username is required.";
            HasEmailError = true;
            valid         = false;
        }
        else if (!Email.Contains('@') && Email.Length < 3)
        {
            EmailError    = "Enter a valid email address or username.";
            HasEmailError = true;
            valid         = false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            PasswordError    = "Password is required.";
            HasPasswordError = true;
            valid            = false;
        }
        else if (Password.Length < 4)
        {
            PasswordError    = "Password must be at least 4 characters.";
            HasPasswordError = true;
            valid            = false;
        }

        return valid;
    }

    // ── Mock Auth ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Simulates authentication. Returns true for the mock credentials
    /// OR any non-empty email + password combination (for demo convenience).
    /// </summary>
    public async Task<bool> AuthenticateAsync()
    {
        IsLoading = true;
        HasError  = false;
        ErrorMessage = string.Empty;

        try
        {
            // Simulate network delay
            await Task.Delay(1200);

            // Accept the known demo account OR any credentials for easy testing
            if (!string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password))
                return true;

            ErrorMessage = "Invalid credentials. Please try again.";
            HasError     = true;
            return false;
        }
        finally
        {
            IsLoading = false;
        }
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
