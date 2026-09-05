using CommunityToolkit.Mvvm.ComponentModel;

namespace AveroNova.App.UI.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool rememberMe;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string emailError = string.Empty;

    [ObservableProperty]
    private bool hasEmailError;

    [ObservableProperty]
    private string passwordError = string.Empty;

    [ObservableProperty]
    private bool hasPasswordError;

    public bool Validate()
    {
        Reset();
        var valid = true;

        if (string.IsNullOrWhiteSpace(Email))
        {
            EmailError = "Email or username is required.";
            HasEmailError = true;
            valid = false;
        }
        else if (!Email.Contains('@') && Email.Length < 3)
        {
            EmailError = "Enter a valid email address or username.";
            HasEmailError = true;
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            PasswordError = "Password is required.";
            HasPasswordError = true;
            valid = false;
        }
        else if (Password.Length < 4)
        {
            PasswordError = "Password must be at least 4 characters.";
            HasPasswordError = true;
            valid = false;
        }

        return valid;
    }

    public void Reset()
    {
        HasError = false;
        HasEmailError = false;
        HasPasswordError = false;
        ErrorMessage = string.Empty;
        EmailError = string.Empty;
        PasswordError = string.Empty;
    }
}
