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

        if (!_installation.CanCreateAccount)
            return (false, "This installation is already registered. Please sign in instead.");

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
        if (!string.IsNullOrWhiteSpace(result.Data.RecoveryKey))
            await _tokens.SetPasswordRecoveryKeyAsync(result.Data.RecoveryKey);
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

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(
        string userEmail,
        string companyEmail,
        string newPassword,
        string confirmPassword)
    {
        if (!_connectivity.IsOnline)
            return (false, "Connect to the internet to reset the password securely on this trusted device.");
        if (!PasswordPolicy.IsStrong(newPassword))
            return (false, PasswordPolicy.RequirementMessage);
        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            return (false, "Passwords do not match.");

        await _installation.EnsureInitializedAsync();
        var recoveryKey = await _tokens.GetPasswordRecoveryKeyAsync();
        if (string.IsNullOrWhiteSpace(recoveryKey))
            return (false, "This device is not currently trusted for direct password reset. Sign in online or contact your administrator.");

        var request = new PasswordResetRequest
        {
            UserEmail = userEmail.Trim(),
            CompanyEmail = companyEmail.Trim(),
            NewPassword = newPassword,
            ConfirmPassword = confirmPassword,
            InstallationId = _installation.InstallationId,
            DeviceId = _installation.DeviceId,
            RecoveryKey = recoveryKey
        };
        var result = await _authApi.ResetPasswordAsync(request);
        if (!result.Success || result.Data is null)
            return (false, result.Error ?? "Unable to reset password.");

        await _tokens.SetPasswordRecoveryKeyAsync(result.Data.RecoveryKey);

        var user = await _sessions.FindUserByEmailAsync(userEmail);
        if (user is not null)
            await StoreLocalCredentialAsync(user.Id, userEmail, newPassword);

        await _tokens.ClearAsync();
        await _sessions.ClearAuthSessionAsync();
        _context.Clear();
        return (true, null);
    }

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
        {
            var refresh = await TryRefreshAccessTokenAsync();
            if (refresh == RefreshOutcome.Rejected)
            {
                await _tokens.ClearAsync();
                await _sessions.ClearAuthSessionAsync();
                _context.Clear();
                return false;
            }
        }

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
            Platform = _device.Platform,
            InstallationId = _installation.InstallationId
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
        if (!string.IsNullOrWhiteSpace(login.RecoveryKey))
            await _tokens.SetPasswordRecoveryKeyAsync(login.RecoveryKey);
        await _sessions.SaveFromLoginAsync(login, _installation.InstallationId);
        _context.SetFromLogin(login);

        if (!_installation.IsRegistered)
            await _installation.MarkRegisteredAsync(login.User.Id, login.CurrentCompany.Id);
    }

    private async Task<RefreshOutcome> TryRefreshAccessTokenAsync()
    {
        try
        {
            var refresh = await _tokens.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refresh))
                return RefreshOutcome.Rejected;

            var expiry = await _tokens.GetAccessTokenExpiryAsync();
            if (expiry is DateTime exp && exp > DateTime.UtcNow.AddMinutes(2))
                return RefreshOutcome.Valid; // still fresh

            var sessionId = await _tokens.GetSessionIdAsync();
            var result = await _authApi.RefreshAsync(new RefreshRequest
            {
                RefreshToken = refresh,
                SessionId = sessionId,
                DeviceId = _installation.DeviceId
            });

            if (!result.Success || result.Data is null)
                return result.IsNetworkError ? RefreshOutcome.NetworkUnavailable : RefreshOutcome.Rejected;

            await PersistAuthenticatedSessionAsync(result.Data);
            return RefreshOutcome.Valid;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token refresh failed.");
            return RefreshOutcome.NetworkUnavailable;
        }
    }

    private enum RefreshOutcome
    {
        Valid,
        NetworkUnavailable,
        Rejected
    }
}
