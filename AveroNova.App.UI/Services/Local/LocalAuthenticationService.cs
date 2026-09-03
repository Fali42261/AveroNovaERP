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

        var normalizedEmail = NormalizeEmail(email);
        if (!IsValidEmail(normalizedEmail))
            return (false, "Please enter a valid email address.");
        if (string.IsNullOrWhiteSpace(password))
            return (false, "Password is required.");

        await using var db = await _factory.CreateDbContextAsync();
        var user = await FindUserByLoginEmailAsync(db, normalizedEmail);

        if (user == null || !user.IsActiveUser || !LocalPasswordHasher.Verify(password, user.PasswordHash))
            return (false, AuthenticationMessages.InvalidEmailOrPassword);

        // Resolve user's company independently of subscription status
        // Subscription restrictions are applied later when accessing protected features
        var companyId = await ResolveUserCompanyAsync(db, user.Id, LocalSessionStore.CompanyId);
        if (companyId == Guid.Empty)
            return (false, AuthenticationMessages.InvalidEmailOrPassword);

        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId && !c.IsDeleted);
        if (company == null)
            return (false, AuthenticationMessages.InvalidEmailOrPassword);

        var roleName = await GetRoleNameAsync(db, user.Id, company.Id);
        _currentUser = MapUser(user, company, roleName);
        LocalSessionStore.Set(user.Id, company.Id, user.Email);
        LocalSessionStore.MarkLocalAccountExists();
        _access.Invalidate();

        System.Diagnostics.Debug.WriteLine(
            $"[swapdigit] Login OK user={user.Id} userEmail={user.Email} company={company.Id}");

        return (true, null);
    }

    public Task<(bool Success, string? Error)> RegisterAsync(string name, string email, string password)
        => Task.FromResult<(bool, string?)>((false, "Please complete all registration steps."));

    public async Task<RegistrationResult> RegisterAccountAsync(RegistrationRequest request)
    {
        await _initializer.EnsureInitializedAsync();

        if (request == null)
            return RegistrationResult.Fail("Registration details are required.");

        var email = NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(request.FullName))
            return RegistrationResult.Fail("Full name is required.");
        if (!IsValidEmail(email))
            return RegistrationResult.Fail("Please enter a valid email address.");
        if (string.IsNullOrWhiteSpace(request.Password))
            return RegistrationResult.Fail("Password is required.");
        if (string.IsNullOrWhiteSpace(request.CompanyName))
            return RegistrationResult.Fail("Company name is required.");
        if (string.IsNullOrWhiteSpace(request.OwnerName))
            return RegistrationResult.Fail("Owner name is required.");

        await using var db = await _factory.CreateDbContextAsync();

        if (await db.Users.AnyAsync(u => !u.IsDeleted && u.Email == email))
            return RegistrationResult.Fail("An account with this email already exists. Please sign in or reset the password.");

        var admin = await db.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == "Administrator" && !r.IsDeleted);
        if (admin == null)
            return RegistrationResult.Fail("Local account setup is incomplete: Administrator role is missing.");

        var freeTrialPlan = await db.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == SubscriptionPlanCodes.FreeTrial && !p.IsDeleted);
        if (freeTrialPlan == null)
            return RegistrationResult.Fail("Local account setup is incomplete: Free Trial plan is missing.");

        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var userCompanyId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var userRoleId = Guid.NewGuid();
        var dbPath = db.Database.GetDbConnection().DataSource;

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var user = new User
            {
                Id = userId,
                UserCode = UniqueCode("U"),
                FullName = Clamp(request.FullName, 150),
                Email = Clamp(email, 150),
                MobileNumber = Clamp(FirstNonEmpty(request.Mobile, "0000000000"), 15),
                PasswordHash = LocalPasswordHasher.Hash(request.Password),
                IsActiveUser = true,
                UserImg = string.Empty,
                CreatedAt = now,
                IsDeleted = false
            };
            db.Users.Add(user);
            await SaveRegistrationStageAsync(db, "User", dbPath);

            var company = new Company
            {
                Id = companyId,
                UserId = userId,
                CompanyCode = UniqueCode("C"),
                CompanyName = Clamp(request.CompanyName, 200),
                OwnerName = Clamp(request.OwnerName, 150),
                GSTNumber = Clamp(request.GSTNumber, 20),
                PANNumber = Clamp(request.PANNumber, 20),
                Email = Clamp(FirstNonEmpty(request.CompanyEmail, request.Email, "noemail@example.com"), 150),
                MobileNumber = Clamp(FirstNonEmpty(request.CompanyMobile, request.Mobile, "0000000000"), 15),
                Address = Clamp(request.Address, 500),
                City = Clamp(request.City, 100),
                State = Clamp(request.State, 100),
                Country = Clamp(request.Country, 100),
                PinCode = Clamp(request.PinCode, 10),
                CreatedAt = now,
                IsDeleted = false
            };
            db.Companies.Add(company);
            await SaveRegistrationStageAsync(db, "Company", dbPath);

            var userCompany = UserCompanyFactory.CreateOwner(userId, companyId, now);
            userCompany.Id = userCompanyId;
            db.UserCompanies.Add(userCompany);
            await SaveRegistrationStageAsync(db, "UserCompany", dbPath);

            var subscription = FreeTrialSubscriptionFactory.Create(companyId, freeTrialPlan, now);
            subscription.Id = subscriptionId;
            db.Subscriptions.Add(subscription);
            await SaveRegistrationStageAsync(db, "Subscription", dbPath);

            db.UserRoles.Add(new UserRole
            {
                Id = userRoleId,
                UserId = userId,
                RoleId = admin.Id,
                CompanyId = companyId,
                CreatedAt = now,
                IsDeleted = false
            });
            await SaveRegistrationStageAsync(db, "UserRole", dbPath);

            var persistedUser = await db.Users.AsNoTracking()
                .AnyAsync(u => u.Id == userId && !u.IsDeleted);
            var persistedCompany = await db.Companies.AsNoTracking()
                .AnyAsync(c => c.Id == companyId && c.UserId == userId && !c.IsDeleted);
            var persistedUserCompany = await db.UserCompanies.AsNoTracking()
                .AnyAsync(uc => uc.UserId == userId && uc.CompanyId == companyId && uc.IsActive && !uc.IsDeleted);
            var persistedSubscription = await db.Subscriptions.AsNoTracking()
                .AnyAsync(s => s.Id == subscriptionId && s.CompanyId == companyId && !s.IsDeleted);
            var persistedUserRole = await db.UserRoles.AsNoTracking()
                .AnyAsync(ur => ur.UserId == userId && ur.RoleId == admin.Id && ur.CompanyId == companyId && !ur.IsDeleted);

            if (!persistedUser || !persistedCompany || !persistedUserCompany || !persistedSubscription || !persistedUserRole)
                throw new InvalidOperationException(
                    $"Registration persistence verification failed. " +
                    $"User={persistedUser}, Company={persistedCompany}, UserCompany={persistedUserCompany}, " +
                    $"Subscription={persistedSubscription}, UserRole={persistedUserRole}.");

            await tx.CommitAsync();

            _currentUser = null;
            LocalSessionStore.ClearSession();
            TrialReminderState.ClearSession();
            LocalSessionStore.MarkLocalAccountExists();

            System.Diagnostics.Debug.WriteLine(
                $"[swapdigit] LOCAL ACCOUNT CREATED path={dbPath} User={userId} Company={companyId} " +
                $"UserCompany={userCompanyId} Subscription={subscriptionId} UserRole={userRoleId}");

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
        catch (Exception ex)
        {
            try
            {
                await tx.RollbackAsync();
            }
            catch (Exception rollbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"[swapdigit] Registration rollback failed: {rollbackEx}");
            }

            LogException("Registration failed", ex, dbPath);

            var rootMessage = GetInnermostMessage(ex);
            if (rootMessage.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                && rootMessage.Contains("Users.Email", StringComparison.OrdinalIgnoreCase))
            {
                return RegistrationResult.Fail("An account with this email already exists. Please sign in or reset the password.");
            }

            if (rootMessage.Contains("FOREIGN KEY constraint failed", StringComparison.OrdinalIgnoreCase))
                return RegistrationResult.Fail("Local account setup is incomplete. Please restart the app and try again.");

            return RegistrationResult.Fail("Account could not be created due to a local database error. Please try again.");
        }
    }

    public async Task<(bool Success, string? Error)> ForgotPasswordAsync(string email)
    {
        await _initializer.EnsureInitializedAsync();

        var normalizedEmail = NormalizeEmail(email);
        if (!IsValidEmail(normalizedEmail))
            return (false, "Please enter a valid email address.");

        await using var db = await _factory.CreateDbContextAsync();
        var user = await FindUserByLoginEmailAsync(db, normalizedEmail);
        if (user == null || !user.IsActiveUser)
            return (false, "No active local account was found for this email address.");

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(string email, string newPassword)
    {
        await _initializer.EnsureInitializedAsync();

        var normalizedEmail = NormalizeEmail(email);
        if (!IsValidEmail(normalizedEmail))
            return (false, "Please enter a valid email address.");
        if (string.IsNullOrWhiteSpace(newPassword))
            return (false, "New password is required.");
        if (newPassword.Length < 8)
            return (false, "New password must be at least 8 characters.");

        await using var db = await _factory.CreateDbContextAsync();
        var user = await FindUserByLoginEmailAsync(db, normalizedEmail, track: true);

        if (user == null || !user.IsActiveUser)
            return (false, "No active local account was found for this email address.");

        user.PasswordHash = LocalPasswordHasher.Hash(newPassword);
        user.UpdatedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            LogException("Password reset failed", ex, db.Database.GetDbConnection().DataSource);
            return (false, "Unable to update the password in the local account database. Please try again.");
        }

        _currentUser = null;
        LocalSessionStore.ClearSession();
        TrialReminderState.ClearSession();
        _access.Invalidate();

        System.Diagnostics.Debug.WriteLine($"[swapdigit] Password reset user={user.Id} userEmail={user.Email}");
        return (true, null);
    }

    public Task<(bool Success, string? Error)> VerifyOtpAsync(string otp)
        => Task.FromResult<(bool, string?)>((false, "OTP verification is not available in offline mode."));

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

        // Resolve user's company independently of subscription status
        var companyId = await ResolveUserCompanyAsync(db, user.Id, LocalSessionStore.CompanyId);
        if (companyId == Guid.Empty)
        {
            LocalSessionStore.ClearSession();
            _currentUser = null;
            return false;
        }

        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId && !c.IsDeleted);
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

    private static async Task<Guid> ResolveUserCompanyAsync(AppDbContext db, Guid userId, Guid? preferredCompanyId)
    {
        if (preferredCompanyId is Guid preferred && preferred != Guid.Empty)
        {
            var belongs = await db.UserCompanies
                .AsNoTracking()
                .AnyAsync(uc => uc.UserId == userId && uc.CompanyId == preferred && uc.IsActive && !uc.IsDeleted);
            if (belongs)
                return preferred;

            var owns = await db.Companies
                .AsNoTracking()
                .AnyAsync(c => c.Id == preferred && c.UserId == userId && !c.IsDeleted);
            if (owns)
                return preferred;
        }

        var userCompany = await db.UserCompanies
            .AsNoTracking()
            .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.IsActive && !uc.IsDeleted);
        if (userCompany != null)
            return userCompany.CompanyId;

        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId && !c.IsDeleted);
        return company?.Id ?? Guid.Empty;
    }

    private static async Task<User?> FindUserByLoginEmailAsync(AppDbContext db, string email, bool track = false)
    {
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        IQueryable<User> query = track ? db.Users : db.Users.AsNoTracking();
        return await query.FirstOrDefaultAsync(u => !u.IsDeleted && u.Email == normalized);
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

    private static async Task SaveRegistrationStageAsync(AppDbContext db, string stage, string dbPath)
    {
        try
        {
            await db.SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine($"[swapdigit] Registration stage OK stage={stage} path={dbPath}");
        }
        catch (Exception ex)
        {
            LogException($"Registration stage failed stage={stage}", ex, dbPath);
            throw;
        }
    }

    private static void LogException(string operation, Exception ex, string? dbPath)
    {
        System.Diagnostics.Debug.WriteLine($"[swapdigit] {operation} path={dbPath}");
        var current = ex;
        var depth = 0;
        while (current != null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[swapdigit] exception[{depth}] type={current.GetType().FullName} message={current.Message}");
            current = current.InnerException;
            depth++;
        }
        System.Diagnostics.Debug.WriteLine($"[swapdigit] stack={ex.StackTrace}");

        if (ex is DbUpdateException dbUpdateException)
        {
            foreach (var entry in dbUpdateException.Entries)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[swapdigit] DbUpdate entity={entry.Metadata.ClrType.Name} state={entry.State}");
            }
        }
    }

    private static string GetInnermostMessage(Exception ex)
    {
        var current = ex;
        while (current.InnerException != null)
            current = current.InnerException;
        return current.Message ?? string.Empty;
    }

    private static string NormalizeEmail(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static bool IsValidEmail(string? email)
        => !string.IsNullOrWhiteSpace(email)
           && System.Text.RegularExpressions.Regex.IsMatch(
               email.Trim(),
               @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
               System.Text.RegularExpressions.RegexOptions.CultureInvariant);

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

    private static string FirstNonEmpty(string? primary, string? fallback, string? finalFallback = null)
    {
        var value = primary?.Trim();
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        value = fallback?.Trim();
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        return (finalFallback ?? string.Empty).Trim();
    }

    private static string Clamp(string? value, int maxLength)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
