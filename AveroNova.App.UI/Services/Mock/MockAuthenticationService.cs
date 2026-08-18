using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

// ═══════════════════════════════════════════════════════════════
//  MockAuthenticationService
//  UI-phase mock. Returns hardcoded success/failure results.
//
//  TODO: Replace with real JWT-based auth against AveroNova API.
// ═══════════════════════════════════════════════════════════════

public class MockAuthenticationService : IAuthenticationService
{
    private UserModel? _currentUser;

    public UserModel? CurrentUser     => _currentUser;
    public bool       IsAuthenticated => _currentUser != null;

    public Task<(bool Success, string? Error)> LoginAsync(string email, string password, bool rememberMe = false)
    {
        // Mock: any non-empty credentials succeed
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return Task.FromResult((false, "Email and password are required."));

        _currentUser = new UserModel
        {
            Name           = "Admin User",
            Email          = email,
            Phone          = "+1 555-0100",
            Role           = "Administrator",
            AvatarInitials = "AU",
            Status         = UserStatus.Active,
            LastLoginAt    = DateTime.UtcNow,
            SyncStatus     = SyncStatus.Synced
        };

        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Success, string? Error)> RegisterAsync(string name, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return Task.FromResult((false, "All fields are required."));

        _currentUser = new UserModel
        {
            Name           = name,
            Email          = email,
            AvatarInitials = string.Join("", name.Split(' ').Take(2).Select(w => w[0].ToString().ToUpper())),
            Role           = "Administrator",
            Status         = UserStatus.Active
        };

        return Task.FromResult<(bool, string?)>((true, null));
    }

    public async Task<RegistrationResult> RegisterAccountAsync(RegistrationRequest request)
    {
        var (success, error) = await RegisterAsync(request.FullName, request.Email, request.Password);
        if (!success)
            return RegistrationResult.Fail(error ?? "Registration failed.");

        return new RegistrationResult
        {
            Success = true,
            LocalAccountCreated = true,
            ServerSynced = false
        };
    }

    public Task<(bool Success, string? Error)> ForgotPasswordAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Task.FromResult((false, "Email is required."));

        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Success, string? Error)> ResetPasswordAsync(string token, string newPassword)
        => Task.FromResult<(bool, string?)>((true, null));

    public Task<(bool Success, string? Error)> VerifyOtpAsync(string otp)
    {
        if (otp == "123456") return Task.FromResult<(bool, string?)>((true, null));
        return Task.FromResult((false, "Invalid OTP. Please try again."));
    }

    public Task LogoutAsync()
    {
        _currentUser = null;
        return Task.CompletedTask;
    }

    public Task<bool> TryAutoLoginAsync() => Task.FromResult(false);

    public Task<bool> HasLocalUserAsync() => Task.FromResult(_currentUser != null);
}
