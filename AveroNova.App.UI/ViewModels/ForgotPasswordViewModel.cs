using System.Text.RegularExpressions;
using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels;

public partial class ForgotPasswordViewModel : ObservableObject
{
    private readonly IAuthenticationService _auth;

    [ObservableProperty] public partial int Step { get; set; } = 1;
    [ObservableProperty] public partial string Email { get; set; } = string.Empty;
    [ObservableProperty] public partial string Otp { get; set; } = string.Empty;
    [ObservableProperty] public partial string NewPassword { get; set; } = string.Empty;
    [ObservableProperty] public partial string ConfirmPassword { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsPasswordHidden { get; set; } = true;
    [ObservableProperty] public partial bool IsConfirmPasswordHidden { get; set; } = true;
    [ObservableProperty] public partial string ShowPasswordText { get; set; } = "Show";
    [ObservableProperty] public partial string ShowConfirmPasswordText { get; set; } = "Show";
    [ObservableProperty] public partial string ErrorMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasError { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }

    public bool IsEmailStep => Step == 1;
    public bool IsOtpStep => Step == 2;
    public bool IsPasswordStep => Step == 3;
    public bool IsSuccessStep => Step == 4;
    public string PrimaryButtonText => IsBusy ? "Loading..." : Step switch
    {
        1 => "Send Code",
        2 => "Verify Code",
        3 => "Save Password",
        _ => "Go to Login"
    };

    public ForgotPasswordViewModel(IAuthenticationService auth)
    {
        _auth = auth;
    }

    partial void OnStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsEmailStep));
        OnPropertyChanged(nameof(IsOtpStep));
        OnPropertyChanged(nameof(IsPasswordStep));
        OnPropertyChanged(nameof(IsSuccessStep));
        OnPropertyChanged(nameof(PrimaryButtonText));
        NextCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(PrimaryButtonText));
        NextCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordHidden = !IsPasswordHidden;
        ShowPasswordText = IsPasswordHidden ? "Show" : "Hide";
    }

    [RelayCommand]
    private void ToggleConfirmPasswordVisibility()
    {
        IsConfirmPasswordHidden = !IsConfirmPasswordHidden;
        ShowConfirmPasswordText = IsConfirmPasswordHidden ? "Show" : "Hide";
    }

    [RelayCommand(CanExecute = nameof(CanNext))]
    private async Task NextAsync()
    {
        HasError = false;
        ErrorMessage = string.Empty;

        if (!NetworkStatus.HasInternet)
        {
            ShowError(UserMessages.InternetRequired);
            return;
        }

        IsBusy = true;
        try
        {
            switch (Step)
            {
                case 1:
                    await SendCodeAsync();
                    break;
                case 2:
                    await VerifyCodeAsync();
                    break;
                case 3:
                    await SavePasswordAsync();
                    break;
            }
        }
        catch
        {
            ShowError(UserMessages.ServerUnavailable);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanNext() => !IsBusy && Step < 4;

    private async Task SendCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || !Regex.IsMatch(Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            ShowError("Please enter a valid email address.");
            return;
        }

        var (success, error) = await _auth.ForgotPasswordAsync(Email.Trim());
        if (!success)
        {
            ShowError(Friendly(error, "Unable to send a verification code."));
            return;
        }

        Otp = string.Empty;
        Step = 2;
    }

    private async Task VerifyCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(Otp) || Otp.Trim().Length != 6)
        {
            ShowError("Please enter the 6-digit verification code.");
            return;
        }

        var (success, error) = await _auth.VerifyOtpAsync(Otp.Trim());
        if (!success)
        {
            ShowError(Friendly(error, UserMessages.InvalidOtp));
            return;
        }

        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        Step = 3;
    }

    private async Task SavePasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ShowError("Password is required.");
            return;
        }

        if (NewPassword.Length < 6)
        {
            ShowError("Password must be at least 6 characters.");
            return;
        }

        if (string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            ShowError("Please confirm your password.");
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            ShowError(UserMessages.PasswordMismatch);
            return;
        }

        var (success, error) = await _auth.ResetPasswordAsync(Otp.Trim(), NewPassword);
        if (!success)
        {
            ShowError(Friendly(error, "Unable to change your password. Please verify your email again."));
            return;
        }

        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        Step = 4;
    }

    private void ShowError(string message)
    {
        HasError = true;
        ErrorMessage = message;
    }

    private static string Friendly(string? error, string fallback)
    {
        if (string.IsNullOrWhiteSpace(error) || error.Contains("Exception", StringComparison.OrdinalIgnoreCase))
            return fallback;
        return error;
    }
}
