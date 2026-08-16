using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockAuthenticationService : IAuthenticationService
{
    private readonly ConcurrentDictionary<string, StoredAccount> _accounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _otpLock = new();

    private UserModel? _currentUser;
    private string? _pendingResetEmail;
    private string? _pendingOtp;
    private DateTime _otpExpiresUtc;
    private bool _otpVerified;
    private string? _rememberedEmail;

    public UserModel? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;

    public async Task<(bool Success, string? Error)> LoginAsync(string email, string password, bool rememberMe = false)
    {
        await Task.Delay(400);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return (false, "Email and password are required.");

        if (!_accounts.TryGetValue(email.Trim(), out var account))
            return (false, UserMessages.InvalidCredentials);

        if (!FixedTimeEquals(account.PasswordHash, HashSecret(password)))
            return (false, UserMessages.InvalidCredentials);

        _currentUser = ToUser(account);
        _currentUser.LastLoginAt = DateTime.UtcNow;
        _rememberedEmail = rememberMe ? account.Email : null;
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(string name, string email, string password, string? phone = null, string? companyName = null)
    {
        await Task.Delay(500);

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return (false, "All required fields must be completed.");

        var key = email.Trim();
        if (_accounts.ContainsKey(key))
            return (false, UserMessages.DuplicateEmail);

        var account = new StoredAccount
        {
            Name = name.Trim(),
            Email = key,
            Phone = phone?.Trim() ?? string.Empty,
            CompanyName = companyName?.Trim() ?? string.Empty,
            PasswordHash = HashSecret(password)
        };

        if (!_accounts.TryAdd(key, account))
            return (false, UserMessages.DuplicateEmail);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ForgotPasswordAsync(string email)
    {
        await Task.Delay(350);

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return (false, "Please enter a valid email address.");

        if (!_accounts.ContainsKey(email.Trim()))
            return (false, "No account was found for this email address.");

        lock (_otpLock)
        {
            _pendingResetEmail = email.Trim();
            _pendingOtp = "123456";
            _otpExpiresUtc = DateTime.UtcNow.AddMinutes(5);
            _otpVerified = false;
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> VerifyOtpAsync(string otp)
    {
        await Task.Delay(250);
        return VerifyPendingOtp(otp);
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(string token, string newPassword)
    {
        await Task.Delay(350);

        lock (_otpLock)
        {
            if (!_otpVerified || string.IsNullOrWhiteSpace(_pendingResetEmail))
                return (false, "Please verify your email before setting a new password.");

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return (false, "Password must be at least 6 characters.");

            if (!_accounts.TryGetValue(_pendingResetEmail, out var account))
                return (false, UserMessages.InvalidCredentials);

            account.PasswordHash = HashSecret(newPassword);
            _pendingResetEmail = null;
            _pendingOtp = null;
            _otpVerified = false;
        }

        return (true, null);
    }

    public Task LogoutAsync()
    {
        _currentUser = null;
        return Task.CompletedTask;
    }

    public Task<bool> TryAutoLoginAsync()
    {
        if (string.IsNullOrWhiteSpace(_rememberedEmail))
            return Task.FromResult(false);

        if (!_accounts.TryGetValue(_rememberedEmail, out var account))
            return Task.FromResult(false);

        _currentUser = ToUser(account);
        return Task.FromResult(true);
    }

    private (bool Success, string? Error) VerifyPendingOtp(string otp)
    {
        lock (_otpLock)
        {
            if (string.IsNullOrWhiteSpace(_pendingResetEmail) || string.IsNullOrWhiteSpace(_pendingOtp))
                return (false, UserMessages.InvalidOtp);

            if (DateTime.UtcNow > _otpExpiresUtc)
                return (false, UserMessages.ExpiredOtp);

            if (otp.Trim() != _pendingOtp)
                return (false, UserMessages.InvalidOtp);

            _otpVerified = true;
            return (true, null);
        }
    }

    private static UserModel ToUser(StoredAccount account)
    {
        var parts = account.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var initials = string.Join("", parts.Take(2).Select(p => p[0].ToString().ToUpperInvariant()));
        return new UserModel
        {
            Name = account.Name,
            Email = account.Email,
            Phone = account.Phone,
            Role = "Owner",
            AvatarInitials = string.IsNullOrEmpty(initials) ? "A" : initials,
            Status = UserStatus.Active,
            CompanyName = account.CompanyName
        };
    }

    private static string HashSecret(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left);
        var b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private sealed class StoredAccount
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
    }
}
