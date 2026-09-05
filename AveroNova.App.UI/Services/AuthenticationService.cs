using AveroNova.Application.DTOs.Auth;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Api;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Security;
using AveroNova.Shared.Security;
using Microsoft.Extensions.Logging;

namespace AveroNova.App.UI.Services;

/// <summary>
/// Online/offline authentication orchestrator for MAUI.
/// Tokens → SecureStorage; auth context → SQLite; password hashes → SecureStorage (never plaintext in SQLite).
/// </summary>
public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IAuthApiClient _authApi;
    private readonly ISecureTokenStore _tokens;
    private readonly ILocalAuthSessionStore _sessions;
    private readonly IAppSessionContext _context;
    private readonly IConnectivityService _connectivity;
    private readonly IInstallationService _installation;
    private readonly IClientDeviceInfo _device;
    private readonly IOfflineRegistrationStore _offlineRegistration;
    private readonly IPendingRegistrationSecretStore _pendingSecrets;
    private readonly ILocalCredentialStore _credentials;
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IAuthApiClient authApi,
        ISecureTokenStore tokens,
        ILocalAuthSessionStore sessions,
        IAppSessionContext context,
        IConnectivityService connectivity,
        IInstallationService installation,
        IClientDeviceInfo device,
        IOfflineRegistrationStore offlineRegistration,
        IPendingRegistrationSecretStore pendingSecrets,
        ILocalCredentialStore credentials,
        ILogger<AuthenticationService> logger)
    {
        _authApi = authApi;
        _tokens = tokens;
        _sessions = sessions;
        _context = context;
        _connectivity = connectivity;
        _installation = installation;
        _device = device;
        _offlineRegistration = offlineRegistration;
        _pendingSecrets = pendingSecrets;
        _credentials = credentials;
        _logger = logger;
    }

    public UserModel? CurrentUser => _context.CurrentUser;
    public bool IsAuthenticated => _context.IsAuthenticated;

    public async Task<(bool Success, string? Error)> LoginAsync(string email, string password, bool rememberMe = false)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return (false, "Email and password are required.");

        await _installation.EnsureInitializedAsync();

        if (_connectivity.IsOnline)
            return await LoginOnlineAsync(email.Trim(), password);

        return await LoginOfflineAsync(email.Trim(), password);
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(RegisterRequest request)
    {
        await _installation.EnsureInitializedAsync();

        if (!PasswordPolicy.IsStrong(request.Password))
            return (false, PasswordPolicy.RequirementMessage);

        request.InstallationId = _installation.InstallationId;
        request.DeviceId = _installation.DeviceId;
        request.DeviceName = _device.Name;
        request.Platform = _device.Platform;

        if (!_connectivity.IsOnline)
            return await RegisterOfflineAsync(request);

        var result = await _authApi.RegisterAsync(request);
        if (result.IsNetworkError)
        {
            _logger.LogWarning("Registration API is unavailable; saving the account locally for later sync.");
            return await RegisterOfflineAsync(request);
        }

        if (!result.Success || result.Data is null)
            return (false, result.Error ?? "Registration failed. Please try again.");

        await _installation.MarkRegisteredAsync(result.Data.UserId, result.Data.CompanyId);
        _context.Clear();
        return (true, null);
    }

    private async Task<(bool Success, string? Error)> RegisterOfflineAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.CompanyName))
            return (false, "Please complete all required registration fields.");

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            return (false, "Passwords do not match.");

        // Stable client IDs so Local SQLite and Server SQLite share identity after sync.
        request.ClientUserId ??= Guid.NewGuid();
        request.ClientCompanyId ??= Guid.NewGuid();
        request.ClientUserCompanyId ??= Guid.NewGuid();
        request.ClientSubscriptionId ??= Guid.NewGuid();

        var ids = await _offlineRegistration.SaveOfflineRegistrationAsync(request, _installation.InstallationId);
        await _pendingSecrets.SetPendingPasswordAsync(ids.UserId, request.Password);
        await StoreLocalCredentialAsync(ids.UserId, request.Email, request.Password);
        await _installation.MarkRegisteredAsync(ids.UserId, ids.CompanyId);
        _context.Clear();
        _logger.LogInformation(
            "Offline registration saved locally UserId={UserId} CompanyId={CompanyId}. Pending sync.",
            ids.UserId, ids.CompanyId);
        return (true, null);
    }

    // Compatibility shim used by older call sites — prefer RegisterAsync(RegisterRequest).
    public Task<(bool Success, string? Error)> RegisterAsync(string name, string email, string password)
        => Task.FromResult<(bool, string?)>((false, "Please complete the full Create Account form."));

    public Task<(bool Success, string? Error)> ForgotPasswordAsync(string email)
        => Task.FromResult<(bool, string?)>((false, "Password reset will be available in a later update."));

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(string email, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (false, "Email address is required.");
        if (!PasswordPolicy.IsStrong(newPassword))
            return (false, PasswordPolicy.RequirementMessage);

        await _installation.EnsureInitializedAsync();

        var normalizedEmail = email.Trim();
        var userId = await _credentials.FindUserIdByEmailAsync(normalizedEmail);
        if (userId is null)
            userId = (await _sessions.FindUserByEmailAsync(normalizedEmail))?.Id;

        if (userId is null)
            return (false, "No local account was found for this email.");

        await StoreLocalCredentialAsync(userId.Value, normalizedEmail, newPassword);

        // An account created offline has not reached the server yet. Keep its
        // pending registration secret aligned with the newly chosen password.
        if (await _pendingSecrets.GetPendingPasswordAsync(userId.Value) is not null)
            await _pendingSecrets.SetPendingPasswordAsync(userId.Value, newPassword);

        await _tokens.ClearAsync();
        await _sessions.ClearAuthSessionAsync();
        _context.Clear();
        _logger.LogInformation("Local password reset completed for UserId={UserId}.", userId.Value);
        return (true, null);
    }

    public Task<(bool Success, string? Error)> VerifyOtpAsync(string otp)
        => Task.FromResult<(bool, string?)>((false, "Verification codes are not used."));

    public async Task LogoutAsync()
    {
        try
        {
            if (_connectivity.IsOnline)
            {
                var access = await _tokens.GetAccessTokenAsync();
                var refresh = await _tokens.GetRefreshTokenAsync();
                var sessionId = await _tokens.GetSessionIdAsync();
                if (!string.IsNullOrWhiteSpace(access))
                {
                    await _authApi.LogoutAsync(
                        new LogoutRequest { SessionId = sessionId, RefreshToken = refresh },
                        access);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Logout API call failed; clearing local auth state anyway.");
        }

        await _tokens.ClearAsync();
        await _sessions.ClearAuthSessionAsync();
        _context.Clear();
    }

    public async Task<bool> TryAutoLoginAsync()
    {
        await _installation.EnsureInitializedAsync();
        if (!_installation.IsRegistered)
            return false;

        var snapshot = await _sessions.LoadValidSessionAsync(_installation.InstallationId);
        if (snapshot is null)
            return false;

        _context.SetFromLocal(
            snapshot.User,
            snapshot.Company,
            snapshot.Roles,
            snapshot.Permissions,
            snapshot.Session.ServerSessionId);

        if (_connectivity.IsOnline)
            await TryRefreshAccessTokenAsync();

        return true;
    }

    public async Task<(bool Success, string? Error)> RefreshTokenAsync()
    {
        if (!_connectivity.IsOnline)
            return (false, "Internet connection is required to refresh your session.");

        var ok = await TryRefreshAccessTokenAsync();
        return ok
            ? (true, null)
            : (false, "Your session could not be refreshed. Please sign in again.");
    }

    private async Task<(bool Success, string? Error)> LoginOnlineAsync(string email, string password)
    {
        var request = new LoginRequest
        {
            Email = email,
            Password = password,
            DeviceId = _installation.DeviceId,
            DeviceName = _device.Name,
            Platform = _device.Platform
        };

        var result = await _authApi.LoginAsync(request);
        if (!result.Success || result.Data is null)
        {
            if (result.IsNetworkError)
            {
                _logger.LogWarning("Login API is unavailable; attempting local authentication.");
                return await LoginOfflineAsync(email, password);
            }
            return (false, result.Error ?? "Invalid email or password.");
        }

        await PersistAuthenticatedSessionAsync(result.Data);
        await StoreLocalCredentialAsync(result.Data.User.Id, email, password);
        return (true, null);
    }

    private async Task<(bool Success, string? Error)> LoginOfflineAsync(string email, string password)
    {
        var userId = await _credentials.FindUserIdByEmailAsync(email);
        if (userId is null)
        {
            var localUser = await _sessions.FindUserByEmailAsync(email);
            userId = localUser?.Id;
        }

        if (userId is null)
        {
            if (await _sessions.HasExpiredSessionAsync(_installation.InstallationId))
                return (false, "Your offline session has expired. Sign in with your local password, or connect to the internet if this device has no saved credentials.");

            return (false, "No local account was found for this email. Create an account or connect to the internet to sign in.");
        }

        var hash = await _credentials.GetPasswordHashAsync(userId.Value);
        if (string.IsNullOrWhiteSpace(hash) || !_hasher.VerifyPassword(password, hash))
            return (false, "Invalid email or password.");

        var snapshot = await _sessions.EstablishOfflineSessionAsync(
            _installation.InstallationId,
            _installation.DeviceId,
            userId.Value);
        if (snapshot is null)
            return (false, "Local account context is incomplete. Please try again.");

        _context.SetFromLocal(
            snapshot.User,
            snapshot.Company,
            snapshot.Roles,
            snapshot.Permissions,
            snapshot.Session.ServerSessionId);
        return (true, null);
    }

    private Task StoreLocalCredentialAsync(Guid userId, string email, string password)
        => _credentials.SetPasswordHashAsync(userId, email, _hasher.HashPassword(password));

    private async Task PersistAuthenticatedSessionAsync(LoginResponse login)
    {
        await _tokens.SetAccessTokenAsync(login.AccessToken, login.AccessTokenExpiresAtUtc);
        await _tokens.SetRefreshTokenAsync(login.RefreshToken);
        await _tokens.SetSessionIdAsync(login.Session.SessionId);
        await _sessions.SaveFromLoginAsync(login, _installation.InstallationId);
        _context.SetFromLogin(login);

        if (!_installation.IsRegistered)
            await _installation.MarkRegisteredAsync(login.User.Id, login.CurrentCompany.Id);
    }

    private async Task<bool> TryRefreshAccessTokenAsync()
    {
        try
        {
            var refresh = await _tokens.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refresh))
                return false;

            var expiry = await _tokens.GetAccessTokenExpiryAsync();
            if (expiry is DateTime exp && exp > DateTime.UtcNow.AddMinutes(2))
                return true; // still fresh

            var sessionId = await _tokens.GetSessionIdAsync();
            var result = await _authApi.RefreshAsync(new RefreshRequest
            {
                RefreshToken = refresh,
                SessionId = sessionId,
                DeviceId = _installation.DeviceId
            });

            if (!result.Success || result.Data is null)
                return false;

            await PersistAuthenticatedSessionAsync(result.Data);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token refresh failed.");
            return false;
        }
    }
}
