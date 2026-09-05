using AveroNova.Application.DTOs.Auth;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.Shared.Security;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public interface IAppSessionContext
{
    UserModel? CurrentUser { get; }
    AuthCompanyDto? CurrentCompany { get; }
    Guid? CurrentUserId { get; }
    Guid? CurrentCompanyId { get; }
    Guid? ServerSessionId { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> Permissions { get; }
    bool IsAuthenticated { get; }
    bool HasPermission(string permissionName);
    event EventHandler? SessionChanged;
    void SetFromLogin(LoginResponse login);
    void SetFromLocal(
        LocalUserEntity user,
        LocalCompanyEntity company,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions,
        Guid serverSessionId);
    void Clear();
}

public sealed class AppSessionContext : IAppSessionContext
{
    private List<string> _roles = [];
    private List<string> _permissions = [];

    public UserModel? CurrentUser { get; private set; }
    public AuthCompanyDto? CurrentCompany { get; private set; }
    public Guid? CurrentUserId => CurrentUser?.LocalId == Guid.Empty ? null : CurrentUser?.LocalId;
    public Guid? CurrentCompanyId => CurrentCompany?.Id;
    public Guid? ServerSessionId { get; private set; }
    public IReadOnlyList<string> Roles => _roles;
    public IReadOnlyList<string> Permissions => _permissions;
    public bool IsAuthenticated => CurrentUser is not null && CurrentCompany is not null;
    public event EventHandler? SessionChanged;

    public bool HasPermission(string permissionName)
        => _permissions.Contains(permissionName, StringComparer.OrdinalIgnoreCase);

    public void SetFromLogin(LoginResponse login)
    {
        CurrentUser = new UserModel
        {
            LocalId = login.User.Id,
            Name = login.User.FullName,
            Email = login.User.Email,
            Phone = login.User.MobileNumber,
            CompanyId = login.CurrentCompany.Id,
            CompanyName = login.CurrentCompany.CompanyName,
            Role = login.Roles.FirstOrDefault() ?? string.Empty,
            AvatarInitials = Initials(login.User.FullName),
            Status = UserStatus.Active,
            LastLoginAt = DateTime.UtcNow,
            SyncStatus = SyncStatus.Synced
        };
        CurrentCompany = login.CurrentCompany;
        ServerSessionId = login.Session.SessionId;
        _roles = login.Roles.ToList();
        _permissions = login.Permissions.ToList();
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetFromLocal(
        LocalUserEntity user,
        LocalCompanyEntity company,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions,
        Guid serverSessionId)
    {
        CurrentUser = new UserModel
        {
            LocalId = user.Id,
            Name = user.FullName,
            Email = user.Email,
            Phone = user.MobileNumber,
            CompanyId = company.Id,
            CompanyName = company.CompanyName,
            Role = roles.FirstOrDefault() ?? string.Empty,
            AvatarInitials = Initials(user.FullName),
            Status = user.IsActive ? UserStatus.Active : UserStatus.Inactive,
            SyncStatus = SyncStatus.Local
        };
        CurrentCompany = new AuthCompanyDto
        {
            Id = company.Id,
            CompanyName = company.CompanyName,
            Email = company.Email,
            MobileNumber = company.MobileNumber
        };
        ServerSessionId = serverSessionId;
        _roles = roles.ToList();
        _permissions = permissions.ToList();
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        CurrentUser = null;
        CurrentCompany = null;
        ServerSessionId = null;
        _roles = [];
        _permissions = [];
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "U";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
    }
}

public interface ILocalAuthSessionStore
{
    Task SaveFromLoginAsync(LoginResponse login, Guid installationId, CancellationToken cancellationToken = default);
    Task<LocalAuthSnapshot?> LoadValidSessionAsync(Guid installationId, string? emailFilter = null, CancellationToken cancellationToken = default);
    Task<LocalAuthSnapshot?> EstablishOfflineSessionAsync(Guid installationId, string deviceId, Guid userId, Guid? preferredCompanyId = null, CancellationToken cancellationToken = default);
    Task<LocalAuthSnapshot?> SwitchCompanyAsync(Guid installationId, Guid userId, Guid companyId, CancellationToken cancellationToken = default);
    Task<bool> HasExpiredSessionAsync(Guid installationId, CancellationToken cancellationToken = default);
    Task ClearAuthSessionAsync(CancellationToken cancellationToken = default);
    Task<List<LocalCompanyEntity>> GetCompaniesForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<LocalUserEntity?> FindUserByEmailAsync(string email, CancellationToken cancellationToken = default);
}

public sealed class LocalAuthSnapshot
{
    public required LocalSessionEntity Session { get; init; }
    public required LocalUserEntity User { get; init; }
    public required LocalCompanyEntity Company { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
    public required IReadOnlyList<string> Permissions { get; init; }
}

public sealed class LocalAuthSessionStore : ILocalAuthSessionStore
{
    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;

    public LocalAuthSessionStore(IDbContextFactory<LocalAppDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task SaveFromLoginAsync(LoginResponse login, Guid installationId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        // Deactivate previous sessions for this installation (auth state only).
        var prior = await db.Sessions
            .Where(s => s.InstallationId == installationId && s.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var s in prior)
        {
            s.IsActive = false;
            s.LastValidatedAtUtc = now;
        }

        UpsertUser(db, login.User);
        foreach (var company in login.Companies
                     .Append(login.CurrentCompany)
                     .GroupBy(c => c.Id)
                     .Select(g => g.First()))
            UpsertCompany(db, company);

        var memberships = await db.UserCompanies
            .Where(uc => uc.UserId == login.User.Id)
            .ToListAsync(cancellationToken);
        db.UserCompanies.RemoveRange(memberships);
        foreach (var company in login.Companies)
        {
            db.UserCompanies.Add(new LocalUserCompanyEntity
            {
                Id = Guid.NewGuid(),
                UserId = login.User.Id,
                CompanyId = company.Id,
                IsDefault = company.IsDefault || company.Id == login.CurrentCompany.Id,
                IsOwner = company.IsOwner,
                IsActive = true
            });
        }

        var oldRoles = await db.Roles
            .Where(r => r.UserId == login.User.Id && r.CompanyId == login.CurrentCompany.Id)
            .ToListAsync(cancellationToken);
        db.Roles.RemoveRange(oldRoles);
        foreach (var role in login.Roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            db.Roles.Add(new LocalRoleEntity
            {
                Id = Guid.NewGuid(),
                UserId = login.User.Id,
                CompanyId = login.CurrentCompany.Id,
                RoleName = role
            });
        }

        var oldPerms = await db.Permissions
            .Where(p => p.UserId == login.User.Id && p.CompanyId == login.CurrentCompany.Id)
            .ToListAsync(cancellationToken);
        db.Permissions.RemoveRange(oldPerms);
        foreach (var perm in login.Permissions.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            db.Permissions.Add(new LocalPermissionEntity
            {
                Id = Guid.NewGuid(),
                UserId = login.User.Id,
                CompanyId = login.CurrentCompany.Id,
                PermissionName = perm
            });
        }

        var offlineExpires = login.Session.OfflineSessionExpiresAtUtc;
        if (offlineExpires <= now)
            offlineExpires = now.Add(OfflineSessionDefaults.OfflineSessionMaxAge);

        db.Sessions.Add(new LocalSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = login.User.Id,
            CompanyId = login.CurrentCompany.Id,
            InstallationId = installationId,
            ServerSessionId = login.Session.SessionId,
            DeviceId = login.Session.DeviceId,
            EstablishedAtUtc = now,
            LastAuthenticatedAtUtc = now,
            LastValidatedAtUtc = now,
            OfflineExpiresAtUtc = offlineExpires,
            IsActive = true
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<LocalAuthSnapshot?> LoadValidSessionAsync(
        Guid installationId,
        string? emailFilter = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var session = await db.Sessions.AsNoTracking()
            .Where(s => s.IsActive
                        && s.InstallationId == installationId
                        && s.OfflineExpiresAtUtc > now)
            .OrderByDescending(s => s.LastAuthenticatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
            return null;

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == session.UserId && u.IsActive, cancellationToken);
        if (user is null)
            return null;

        if (!string.IsNullOrWhiteSpace(emailFilter)
            && !string.Equals(user.Email, emailFilter.Trim(), StringComparison.OrdinalIgnoreCase))
            return null;

        var company = await db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == session.CompanyId && c.IsActive, cancellationToken);
        if (company is null)
            return null;

        var membership = await db.UserCompanies.AsNoTracking()
            .AnyAsync(uc => uc.UserId == user.Id && uc.CompanyId == company.Id && uc.IsActive, cancellationToken);
        if (!membership)
            return null;

        var roles = await db.Roles.AsNoTracking()
            .Where(r => r.UserId == user.Id && r.CompanyId == company.Id)
            .Select(r => r.RoleName)
            .ToListAsync(cancellationToken);

        var permissions = await db.Permissions.AsNoTracking()
            .Where(p => p.UserId == user.Id && p.CompanyId == company.Id)
            .Select(p => p.PermissionName)
            .ToListAsync(cancellationToken);

        if (permissions.Count == 0)
            return null;

        return new LocalAuthSnapshot
        {
            Session = session,
            User = user,
            Company = company,
            Roles = roles,
            Permissions = permissions
        };
    }

    public async Task<LocalAuthSnapshot?> EstablishOfflineSessionAsync(
        Guid installationId,
        string deviceId,
        Guid userId,
        Guid? preferredCompanyId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);
        if (user is null)
            return null;

        var memberships = await db.UserCompanies
            .Where(uc => uc.UserId == userId && uc.IsActive)
            .ToListAsync(cancellationToken);
        if (memberships.Count == 0)
            return null;

        var membership = preferredCompanyId is Guid preferred
            ? memberships.FirstOrDefault(m => m.CompanyId == preferred) ?? memberships.FirstOrDefault(m => m.IsDefault) ?? memberships[0]
            : memberships.FirstOrDefault(m => m.IsDefault) ?? memberships[0];

        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == membership.CompanyId && c.IsActive, cancellationToken);
        if (company is null)
            return null;

        var roles = await db.Roles
            .Where(r => r.UserId == user.Id && r.CompanyId == company.Id)
            .Select(r => r.RoleName)
            .ToListAsync(cancellationToken);
        var permissions = await db.Permissions
            .Where(p => p.UserId == user.Id && p.CompanyId == company.Id)
            .Select(p => p.PermissionName)
            .ToListAsync(cancellationToken);

        if (permissions.Count == 0)
            return null;

        var now = DateTime.UtcNow;
        var prior = await db.Sessions
            .Where(s => s.InstallationId == installationId && s.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var s in prior)
        {
            s.IsActive = false;
            s.LastValidatedAtUtc = now;
        }

        var session = new LocalSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CompanyId = company.Id,
            InstallationId = installationId,
            ServerSessionId = Guid.NewGuid(),
            DeviceId = deviceId,
            EstablishedAtUtc = now,
            LastAuthenticatedAtUtc = now,
            LastValidatedAtUtc = now,
            OfflineExpiresAtUtc = now.Add(OfflineSessionDefaults.OfflineSessionMaxAge),
            IsActive = true
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return new LocalAuthSnapshot
        {
            Session = session,
            User = user,
            Company = company,
            Roles = roles,
            Permissions = permissions
        };
    }

    public async Task<LocalAuthSnapshot?> SwitchCompanyAsync(
        Guid installationId,
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var membership = await db.UserCompanies.AsNoTracking()
            .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CompanyId == companyId && uc.IsActive, cancellationToken);
        if (membership is null)
            return null;

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);
        var company = await db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId && c.IsActive, cancellationToken);
        if (user is null || company is null)
            return null;

        var roles = await db.Roles.AsNoTracking()
            .Where(r => r.UserId == userId && r.CompanyId == companyId)
            .Select(r => r.RoleName)
            .ToListAsync(cancellationToken);
        var permissions = await db.Permissions.AsNoTracking()
            .Where(p => p.UserId == userId && p.CompanyId == companyId)
            .Select(p => p.PermissionName)
            .ToListAsync(cancellationToken);
        if (permissions.Count == 0)
            return null;

        var now = DateTime.UtcNow;
        var session = await db.Sessions
            .Where(s => s.InstallationId == installationId && s.IsActive && s.UserId == userId)
            .OrderByDescending(s => s.LastAuthenticatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (session is null)
            return null;

        session.CompanyId = companyId;
        session.LastValidatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        return new LocalAuthSnapshot
        {
            Session = session,
            User = user,
            Company = company,
            Roles = roles,
            Permissions = permissions
        };
    }

    public async Task<List<LocalCompanyEntity>> GetCompaniesForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var companyIds = await db.UserCompanies.AsNoTracking()
            .Where(uc => uc.UserId == userId && uc.IsActive)
            .Select(uc => uc.CompanyId)
            .ToListAsync(cancellationToken);

        return await db.Companies.AsNoTracking()
            .Where(c => companyIds.Contains(c.Id) && c.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<LocalUserEntity?> FindUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var users = await db.Users.AsNoTracking().Where(u => u.IsActive).ToListAsync(cancellationToken);
        return users.FirstOrDefault(u => string.Equals(u.Email, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> HasExpiredSessionAsync(Guid installationId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        return await db.Sessions.AsNoTracking().AnyAsync(
            s => s.InstallationId == installationId && s.OfflineExpiresAtUtc <= now,
            cancellationToken);
    }

    public async Task ClearAuthSessionAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var active = await db.Sessions.Where(s => s.IsActive).ToListAsync(cancellationToken);
        foreach (var s in active)
        {
            s.IsActive = false;
            s.LastValidatedAtUtc = now;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void UpsertUser(LocalAppDbContext db, AuthUserDto user)
    {
        var existing = db.Users.Local.FirstOrDefault(u => u.Id == user.Id)
                       ?? db.Users.FirstOrDefault(u => u.Id == user.Id);
        if (existing is null)
        {
            db.Users.Add(new LocalUserEntity
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                MobileNumber = user.MobileNumber,
                IsActive = user.IsActive
            });
            return;
        }

        existing.FullName = user.FullName;
        existing.Email = user.Email;
        existing.MobileNumber = user.MobileNumber;
        existing.IsActive = user.IsActive;
    }

    private static void UpsertCompany(LocalAppDbContext db, AuthCompanyDto company)
    {
        // Prefer Local tracker: CurrentCompany is often also present in Companies.
        var existing = db.Companies.Local.FirstOrDefault(c => c.Id == company.Id)
                       ?? db.Companies.FirstOrDefault(c => c.Id == company.Id);
        if (existing is null)
        {
            db.Companies.Add(new LocalCompanyEntity
            {
                Id = company.Id,
                CompanyName = company.CompanyName,
                Email = company.Email,
                MobileNumber = company.MobileNumber,
                IsActive = true
            });
            return;
        }

        existing.CompanyName = company.CompanyName;
        existing.Email = company.Email;
        existing.MobileNumber = company.MobileNumber;
        existing.IsActive = true;
    }
}
