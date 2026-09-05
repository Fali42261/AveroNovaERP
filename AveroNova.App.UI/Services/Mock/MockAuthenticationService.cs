using AveroNova.App.UI.Models;
using AveroNova.Application.DTOs.Auth;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

/// <summary>Legacy UI mock — not registered in DI for Phase 4. Kept for reference/tests.</summary>
public class MockAuthenticationService : IAuthenticationService
{
    private UserModel? _currentUser;

    public UserModel? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;

    public Task<(bool Success, string? Error)> LoginAsync(string email, string password, bool rememberMe = false)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return Task.FromResult((false, "Email and password are required."));

        _currentUser = new UserModel
        {
            Name = "Admin User",
            Email = email,
            Role = "Administrator",
            Status = UserStatus.Active,
            SyncStatus = SyncStatus.Synced
        };
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Success, string? Error)> RegisterAsync(RegisterRequest request)
        => Task.FromResult<(bool, string?)>((false, "Mock registration is disabled."));

    public Task<(bool Success, string? Error)> RegisterAsync(string name, string email, string password)
        => Task.FromResult<(bool, string?)>((false, "Mock registration is disabled."));

    public Task<(bool Success, string? Error)> ForgotPasswordAsync(string email)
        => Task.FromResult<(bool, string?)>((true, null));

    public Task<(bool Success, string? Error)> ResetPasswordAsync(string token, string newPassword)
        => Task.FromResult<(bool, string?)>((true, null));

    public Task<(bool Success, string? Error)> VerifyOtpAsync(string otp)
        => Task.FromResult<(bool, string?)>((false, "Invalid OTP."));

    public Task<(bool Success, string? Error)> RefreshTokenAsync()
        => Task.FromResult<(bool, string?)>((false, "Not supported."));

    public Task LogoutAsync()
    {
        _currentUser = null;
        return Task.CompletedTask;
    }

    public Task<bool> TryAutoLoginAsync() => Task.FromResult(false);
}
