using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Application.Interfaces;
using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Services;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services.Local;

/// <summary>
/// Offline-first local authentication against AppDbContext.
/// Session restore never requires the API. Company membership is UserCompany,
/// with Company.UserId kept as the owner link.
/// </summary>
public sealed class LocalAuthenticationService : IAuthenticationService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly LocalDatabaseInitializer _initializer;
    private readonly ICompanySubscriptionService _subscriptions;
    private readonly CurrentAccessService _access;
    private UserModel? _currentUser;

    public LocalAuthenticationService(
        IDbContextFactory<AppDbContext> factory,
        LocalDatabaseInitializer initializer,
        ICompanySubscriptionService subscriptions,
        CurrentAccessService access)
    {
        _factory = factory;
        _initializer = initializer;
        _subscriptions = subscriptions;
        _access = access;
    }

    public UserModel? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;

    public async Task<(bool Success, string? Error)> LoginAsync(string email, string password, bool rememberMe = false)
    {
        await _initializer.EnsureInitializedAsync();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return (false, "Email and password are required.");

        await using var db = await _factory.CreateDbContextAsync();
        var user = await FindUserByLoginEmailAsync(db, email);

        // Authenticate against Users only. Company.Email is never used for login.
        if (user == null || !user.IsActiveUser || !LocalPasswordHasher.Verify(password, user.PasswordHash))
            return (false, AuthenticationMessages.InvalidEmailOrPassword);

        var access = await _subscriptions.ResolveLoginCompanyAsync(user.Id, LocalSessionStore.CompanyId);
        if (!access.IsAllowed || access.CompanyId is not Guid allowedCompanyId || allowedCompanyId == Guid.Empty)
            return (false, access.Message ?? SubscriptionMessages.FreeTrialExpiredAccess);

        var belongsToCompany = await db.UserCompanies
            .AsNoTracking()
            .AnyAsync(uc => uc.UserId == user.Id
                            && uc.CompanyId == allowedCompanyId
                            && uc.IsActive
                            && !uc.IsDeleted);
        if (!belongsToCompany)
        {
            belongsToCompany = await db.Companies
                .AsNoTracking()
                .AnyAsync(c => c.Id == allowedCompanyId && c.UserId == user.Id && !c.IsDeleted);
        }

        if (!belongsToCompany)
            return (false, AuthenticationMessages.InvalidEmailOrPassword);

        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == allowedCompanyId && !c.IsDeleted);
        if (company == null)
            return (false, SubscriptionMessages.FreeTrialExpiredAccess);

        var roleName = await GetRoleNameAsync(db, user.Id, company.Id);
        _currentUser = MapUser(user, company, roleName);
        LocalSessionStore.Set(user.Id, company.Id, user.Email);
        LocalSessionStore.MarkLocalAccountExists();
        _access.Invalidate();

        System.Diagnostics.Debug.WriteLine(
            $"[AveroNova] Login OK user={user.Id} userEmail={user.Email} company={company.Id} via UserCompany");

        return (true, null);
    }

    public Task<(bool Success, string? Error)> RegisterAsync(string name, string email, string password)
        => Task.FromResult<(bool, string?)>((false, "Please complete all registration steps."));

    public async Task<RegistrationResult> RegisterAccountAsync(RegistrationRequest request)
    {
        await _initializer.EnsureInitializedAsync();

        if (request == null)
            return RegistrationResult.Fail("Registration details are required.");

        var email = request.Email.Trim();
        await using var db = await _factory.CreateDbContextAsync();

        if (await db.Users.AnyAsync(u => u.Email == email && !u.IsDeleted))
            return RegistrationResult.Fail("An account with this email already exists.");

        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var userRoleId = Guid.NewGuid();

        var admin = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        if (admin == null)
            return RegistrationResult.Fail("Administrator role is missing from the local database.");

        var freeTrialPlan = await db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Code == SubscriptionPlanCodes.FreeTrial && !p.IsDeleted);
        if (freeTrialPlan == null)
            return RegistrationResult.Fail("Free Trial plan is missing from the local database.");

        var userCompanyId = Guid.NewGuid();
        var dbPath = db.Database.GetDbConnection().DataSource;

        User? user = null;
        Company? company = null;

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            user = new User
            {
                Id = userId,
                UserCode = UniqueCode("U"),
                FullName = Clamp(request.FullName, 150),
                Email = Clamp(email, 150),
                PasswordHash = LocalPasswordHasher.Hash(request.Password),
                IsActiveUser = true,
                CreatedAt = now,
                IsDeleted = false
            };
            db.Users.Add(user);

            company = new Company
            {
                Id = companyId,
                UserId = userId,
                CompanyCode = UniqueCode("C"),
                CompanyName = Clamp(request.CompanyName, 200),
                OwnerName = Clamp(request.OwnerName, 150),
                GSTNumber = Clamp(request.GSTNumber, 20),
                PANNumber = Clamp(request.PANNumber, 20),
                Email = Clamp(FirstNonEmpty(request.CompanyEmail, request.Email), 150),
                MobileNumber = Clamp(FirstNonEmpty(request.CompanyMobile, request.Mobile), 15),
                Address = Clamp(request.Address, 500),
                City = Clamp(request.City, 100),
                State = Clamp(request.State, 100),
                Country = Clamp(request.Country, 100),
                PinCode = Clamp(request.PinCode, 10),
                CreatedAt = now,
                IsDeleted = false
            };
            db.Companies.Add(company);

            var userCompany = UserCompanyFactory.CreateOwner(userId, companyId, now);
            userCompany.Id = userCompanyId;
            db.UserCompanies.Add(userCompany);

            var subscription = FreeTrialSubscriptionFactory.Create(companyId, freeTrialPlan, now);
            subscription.Id = subscriptionId;
            db.Subscriptions.Add(subscription);

            db.UserRoles.Add(new UserRole
            {
                Id = userRoleId,
                UserId = userId,
                RoleId = admin.Id,
                CompanyId = companyId,
                CreatedAt = now,
                IsDeleted = false
            });

            await db.SaveChangesAsync();

            var persistedUser = await db.Users.AnyAsync(u => u.Id == userId && u.Email == user.Email && !u.IsDeleted);
            var persistedCompany = await db.Companies.AnyAsync(
                c => c.Id == companyId && c.UserId == userId && !c.IsDeleted);
            var persistedUserCompany = await db.UserCompanies.AnyAsync(
                uc => uc.UserId == userId && uc.CompanyId == companyId && !uc.IsDeleted);
            var persistedSubscription = await db.Subscriptions.AnyAsync(
                s => s.Id == subscriptionId && s.CompanyId == companyId && !s.IsDeleted);
            var persistedUserRole = await db.UserRoles.AnyAsync(
                ur => ur.UserId == userId && ur.RoleId == admin.Id && ur.CompanyId == companyId && !ur.IsDeleted);
            var permissionCount = await db.RolePermissions.CountAsync(
                rp => rp.RoleId == admin.Id && !rp.IsDeleted);

            if (!persistedUser || !persistedCompany || !persistedUserCompany || !persistedSubscription || !persistedUserRole || permissionCount == 0)
            {
                await tx.RollbackAsync();
                System.Diagnostics.Debug.WriteLine(
                    $"[AveroNova] Registration persistence check failed path={dbPath} " +
                    $"user={persistedUser} company={persistedCompany} userCompany={persistedUserCompany} " +
                    $"subscription={persistedSubscription} userRole={persistedUserRole} permissions={permissionCount}");
                return RegistrationResult.Fail(
                    "Account could not be saved to the local database. The account was not created.");
            }

            await tx.CommitAsync();

            System.Diagnostics.Debug.WriteLine(
                "[AveroNova] LOCAL ACCOUNT CREATED " +
                $"path={dbPath} User={userId} Company={companyId} UserCompany={userCompanyId} " +
                $"Subscription={subscriptionId} Role={admin.Id} RolePermissions={permissionCount} " +
                "ServerSynced=false");
        }
        catch (Exception ex)
        {
            try
            {
                await tx.RollbackAsync();
            }
            catch (Exception rollbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"[AveroNova] Registration rollback failed: {rollbackEx}");
            }

            System.Diagnostics.Debug.WriteLine($"[AveroNova] Registration failed path={dbPath}: {ex}");
            return RegistrationResult.Fail($"Account could not be created. {ex.Message}");
        }

        if (user is null || company is null)
            return RegistrationResult.Fail("Account could not be created.");

        // Stay logged out. Dashboard is reached only after a manual Login.
        _currentUser = null;
        LocalSessionStore.ClearSession();
        TrialReminderState.ClearSession();
        LocalSessionStore.MarkLocalAccountExists();

        System.Diagnostics.Debug.WriteLine(
            "[AveroNova] Registration complete without session. User must sign in manually.");

        return new RegistrationResult
        {
            Success = true,
            LocalAccountCreated = true,
            ServerSynced = false,
            UserId = userId,
            CompanyId = companyId,
            SubscriptionId = subscriptionId,
            RoleId = admin.Id
        };
    }

    public Task<(bool Success, string? Error)> ForgotPasswordAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Task.FromResult((false, "Email is required."));

        // User.Email only. Do not disclose whether the address exists.
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(string email, string newPassword)
    {
        await _initializer.EnsureInitializedAsync();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPassword))
            return (false, "Email and new password are required.");

        await using var db = await _factory.CreateDbContextAsync();

        // Same User.Email lookup as login. Company.Email is never used.
        var user = await FindUserByLoginEmailAsync(db, email, track: true);

        if (user is { IsActiveUser: true })
        {
            user.PasswordHash = LocalPasswordHasher.Hash(newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine(
                $"[AveroNova] User password reset user={user.Id} userEmail={user.Email}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine(
                "[AveroNova] Password reset requested for an email that is not an active User.Email. No password was changed.");
        }

        // Generic result so callers cannot tell whether a User.Email existed.
        return (true, null);
    }

    public Task<(bool Success, string? Error)> VerifyOtpAsync(string otp)
        => Task.FromResult<(bool, string?)>((true, null));

    public Task LogoutAsync()
    {
        _currentUser = null;
        LocalSessionStore.ClearSession();
        TrialReminderState.ClearSession();
        _access.Invalidate();
        return Task.CompletedTask;
    }

    public async Task<bool> TryAutoLoginAsync()
    {
        await _initializer.EnsureInitializedAsync();

        var userId = LocalSessionStore.UserId;
        if (userId == null)
            return false;

        await using var db = await _factory.CreateDbContextAsync();
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value && !u.IsDeleted && u.IsActiveUser);

        if (user == null)
        {
            LocalSessionStore.ClearSession();
            _currentUser = null;
            return false;
        }

        var access = await _subscriptions.ResolveLoginCompanyAsync(user.Id, LocalSessionStore.CompanyId);
        if (!access.IsAllowed || access.CompanyId is not Guid allowedCompanyId || allowedCompanyId == Guid.Empty)
        {
            PendingAuthMessage.Set(access.Message ?? SubscriptionMessages.FreeTrialExpiredAccess);
            LocalSessionStore.ClearSession();
            TrialReminderState.ClearSession();
            _currentUser = null;
            return false;
        }

        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == allowedCompanyId && !c.IsDeleted);
        if (company == null)
        {
            LocalSessionStore.ClearSession();
            _currentUser = null;
            return false;
        }

        var roleName = await GetRoleNameAsync(db, user.Id, company.Id);
        _currentUser = MapUser(user, company, roleName);
        LocalSessionStore.Set(user.Id, company.Id, user.Email);
        _access.Invalidate();

        System.Diagnostics.Debug.WriteLine(
            $"[AveroNova] Session restored user={user.Id} company={company.Id} (no API)");
        return true;
    }

    public async Task<bool> HasLocalUserAsync()
    {
        await _initializer.EnsureInitializedAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var exists = await db.Users.AnyAsync(u => !u.IsDeleted);
        if (exists)
            LocalSessionStore.MarkLocalAccountExists();
        return exists;
    }

    /// <summary>
    /// Resolves the login identity from the Users table by User.Email only.
    /// Company.Email is not consulted.
    /// </summary>
    private static async Task<User?> FindUserByLoginEmailAsync(AppDbContext db, string email, bool track = false)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        IQueryable<User> query = track ? db.Users : db.Users.AsNoTracking();
        return await query.FirstOrDefaultAsync(u => !u.IsDeleted && u.Email.ToLower() == normalized);
    }

    private static async Task<string> GetRoleNameAsync(AppDbContext db, Guid userId, Guid companyId)
    {
        var name = await (
            from ur in db.UserRoles.AsNoTracking()
            join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where ur.UserId == userId
                  && ur.CompanyId == companyId
                  && !ur.IsDeleted
                  && !r.IsDeleted
            select r.Name).FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(name))
            return name;

        name = await (
            from ur in db.UserRoles.AsNoTracking()
            join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where ur.UserId == userId
                  && (ur.CompanyId == null || ur.CompanyId == Guid.Empty)
                  && !ur.IsDeleted
                  && !r.IsDeleted
            select r.Name).FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(name) ? "User" : name;
    }

    private static UserModel MapUser(User user, Company? company, string roleName)
    {
        var name = user.FullName;
        return new UserModel
        {
            LocalId = user.Id,
            Name = name,
            Email = user.Email,
            Phone = string.IsNullOrWhiteSpace(user.MobileNumber)
                ? (company?.MobileNumber ?? string.Empty)
                : user.MobileNumber,
            Role = roleName,
            AvatarInitials = Initials(name),
            CompanyId = company?.Id,
            CompanyName = company?.CompanyName ?? string.Empty,
            Status = user.IsActiveUser ? UserStatus.Active : UserStatus.Inactive,
            LastLoginAt = DateTime.UtcNow,
            SyncStatus = SyncStatus.PendingSync
        };
    }

    private static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "?";
        if (parts.Length == 1)
            return parts[0][0].ToString().ToUpperInvariant();
        return string.Concat(parts[0][0], parts[1][0]).ToUpperInvariant();
    }

    private static string UniqueCode(string prefix)
        => prefix + Convert.ToHexString(Guid.NewGuid().ToByteArray())[..8];

    private static string FirstNonEmpty(string primary, string fallback)
    {
        var value = primary?.Trim();
        return string.IsNullOrWhiteSpace(value) ? (fallback ?? string.Empty).Trim() : value;
    }

    private static string Clamp(string? value, int maxLength)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
