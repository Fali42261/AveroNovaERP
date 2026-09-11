using AveroNova.Application.DTOs.Auth;
using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

// ═══════════════════════════════════════════════════════════════
//  IAuthenticationService
//
//  ONLINE: Login API → Secure tokens + SQLite session → Dashboard
//  OFFLINE: Local credential hash + local session → Dashboard
// ═══════════════════════════════════════════════════════════════

public interface IAuthenticationService
{
    UserModel? CurrentUser { get; }
    bool IsAuthenticated { get; }

    Task<(bool Success, string? Error)> LoginAsync(string email, string password, bool rememberMe = false);
    Task<(bool Success, string? Error)> RegisterAsync(RegisterRequest request);
    Task<(bool Success, string? Error)> RegisterAsync(string name, string email, string password);
    Task<(bool Success, string? Error)> ResetPasswordAsync(
        string userEmail,
        string companyEmail,
        string newPassword,
        string confirmPassword);
    Task<(bool Success, string? Error)> RefreshTokenAsync();
    Task LogoutAsync();
    Task<bool> TryAutoLoginAsync();
}
