using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services.Local;

/// <summary>
/// Offline-first local authentication against AppDbContext.
/// Session restore never requires the API. Company is linked via Company.UserId
/// (existing schema has no UserCompany table).
/// </summary>
public sealed class LocalAuthenticationService : IAuthenticationService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly LocalDatabaseInitializer _initializer;
    private UserModel? _currentUser;

    public LocalAuthenticationService(
        IDbContextFactory<AppDbContext> factory,
        LocalDatabaseInitializer initializer)
    {
        _factory = factory;
        _initializer = initializer;
    }

    public UserModel? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;

    public async Task<(bool Success, string? Error)> LoginAsync(string email, string password, bool rememberMe = false)
    {
        await _initializer.EnsureInitializedAsync();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return (false, "Email and password are required.");

        await using var db = await _factory.CreateDbContextAsync();
        var normalized = email.Trim();
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalized && !u.IsDeleted);

        if (user == null || !LocalPasswordHasher.Verify(password, user.PasswordHash))
            return (false, "Invalid email or password.");

        if (!user.IsActiveUser)
            return (false, "This account is inactive.");

        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == user.Id && !c.IsDeleted);

        var roleName = await GetRoleNameAsync(db, user.Id);
        _currentUser = MapUser(user, company, roleName);
        LocalSessionStore.Set(user.Id, company?.Id ?? Guid.Empty, user.Email);
        LocalSessionStore.MarkLocalAccountExists();

        System.Diagnostics.Debug.WriteLine(
            $"[AveroNova] Login OK user={user.Id} company={company?.Id}");

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

        var planName = string.IsNullOrWhiteSpace(request.PlanName) ? "Starter" : request.PlanName.Trim();
        var duration = (int)SubscriptionPlan.FifteenDays;
        var start = DateTime.UtcNow.Date;
        var expiry = start.AddDays(duration);

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var user = new User
            {
                Id = userId,
                UserCode = UniqueCode("U"),
                FullName = request.FullName.Trim(),
                Email = email,
                PasswordHash = LocalPasswordHasher.Hash(request.Password),
                IsActiveUser = true,
                CreatedAt = now,
                IsDeleted = false
            };
            db.Users.Add(user);

            var company = new Company
            {
                Id = companyId,
                UserId = userId,
                CompanyCode = UniqueCode("C"),
                CompanyName = request.CompanyName.Trim(),
                OwnerName = request.OwnerName.Trim(),
                GSTNumber = request.GSTNumber.Trim(),
                PANNumber = request.PANNumber.Trim(),
                Email = request.CompanyEmail.Trim(),
                MobileNumber = FirstNonEmpty(request.CompanyMobile, request.Mobile),
                Address = request.Address.Trim(),
                City = request.City.Trim(),
                State = request.State.Trim(),
                Country = request.Country.Trim(),
                PinCode = request.PinCode.Trim(),
                CreatedAt = now,
                IsDeleted = false
            };
            db.Companies.Add(company);

            var subscription = new Subscription
            {
                Id = subscriptionId,
                CompanyId = companyId,
                PlanName = planName,
                Price = 0m,
                DurationInDays = duration,
                StartDate = start,
                ExpiryDate = expiry,
                IsSubscription = true,
                Status = AveroNova.Domain.Enums.SubscriptionStatus.Active,
                Plan = SubscriptionPlan.FifteenDays,
                CreatedAt = now,
                IsDeleted = false
            };
            db.Subscriptions.Add(subscription);

            db.UserRoles.Add(new UserRole
            {
                Id = userRoleId,
                UserId = userId,
                RoleId = admin.Id,
                CreatedAt = now,
                IsDeleted = false
            });

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var permissionCount = await db.RolePermissions.CountAsync(rp => rp.RoleId == admin.Id && !rp.IsDeleted);

            System.Diagnostics.Debug.WriteLine(
                "[AveroNova] LOCAL ACCOUNT CREATED " +
                $"User={userId} Company={companyId} UserCompany=Company.UserId " +
                $"Subscription={subscriptionId} Role={admin.Id} RolePermissions={permissionCount} " +
                "ServerSynced=false");

            _currentUser = MapUser(user, company, admin.Name);
            _currentUser.Phone = request.Mobile.Trim();
            LocalSessionStore.Set(userId, companyId, email);

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
            await tx.RollbackAsync();
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Registration failed: {ex}");
            return RegistrationResult.Fail($"Account could not be created. {ex.Message}");
        }
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
        => Task.FromResult<(bool, string?)>((true, null));

    public Task LogoutAsync()
    {
        _currentUser = null;
        LocalSessionStore.ClearSession();
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

        var companyId = LocalSessionStore.CompanyId;
        Company? company = null;
        if (companyId != null)
        {
            company = await db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId.Value && !c.IsDeleted);
        }

        company ??= await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == user.Id && !c.IsDeleted);

        var roleName = await GetRoleNameAsync(db, user.Id);
        _currentUser = MapUser(user, company, roleName);
        if (company != null)
            LocalSessionStore.Set(user.Id, company.Id, user.Email);

        System.Diagnostics.Debug.WriteLine(
            $"[AveroNova] Session restored user={user.Id} company={company?.Id} (no API)");
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

    private static async Task<string> GetRoleNameAsync(AppDbContext db, Guid userId)
    {
        var name = await (
            from ur in db.UserRoles.AsNoTracking()
            join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where ur.UserId == userId && !ur.IsDeleted && !r.IsDeleted
            select r.Name).FirstOrDefaultAsync();
        return string.IsNullOrWhiteSpace(name) ? "Administrator" : name;
    }

    private static UserModel MapUser(User user, Company? company, string roleName)
    {
        var name = user.FullName;
        return new UserModel
        {
            LocalId = user.Id,
            Name = name,
            Email = user.Email,
            Phone = company?.MobileNumber ?? string.Empty,
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
        return string.IsNullOrWhiteSpace(value) ? fallback.Trim() : value;
    }
}
