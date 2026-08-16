using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

// ═══════════════════════════════════════════════════════════════
//  IAuthenticationService
//
//  ONLINE FLOW:
//    ViewModel → IAuthenticationService → API → Server Database
//    The API returns a JWT token stored locally for subsequent calls.
//
//  OFFLINE FLOW:
//    ViewModel → IAuthenticationService → Local cached credentials
//    Offline login uses a locally cached credential hash.
//    The user can work offline; token refresh happens on reconnect.
//
//  TODO: Implement real auth against AveroNova API during backend phase.
// ═══════════════════════════════════════════════════════════════

public interface IAuthenticationService
{
    UserModel? CurrentUser { get; }
    bool       IsAuthenticated { get; }

    Task<(bool Success, string? Error)> LoginAsync(string email, string password, bool rememberMe = false);
    Task<(bool Success, string? Error)> RegisterAsync(string name, string email, string password);
    Task<(bool Success, string? Error)> ForgotPasswordAsync(string email);
    Task<(bool Success, string? Error)> ResetPasswordAsync(string token, string newPassword);
    Task<(bool Success, string? Error)> VerifyOtpAsync(string otp);
    Task                                LogoutAsync();
    Task<bool>                          TryAutoLoginAsync();
}
