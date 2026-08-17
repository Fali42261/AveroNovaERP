using System.Security.Claims;
using AveroNova.Application.Common;
using AveroNova.Application.DTOs.Auth;
using AveroNova.Application.Interfaces.Auth;
using AveroNova.Application.Interfaces.Security;
using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Persistence;
using AveroNova.Shared.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AveroNova.Infrastructure.Auth;

public sealed class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IAuthAuditLogger _audit;
    private readonly ILoginAttemptProtector _attempts;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwt,
        IRefreshTokenService refreshTokens,
        IAuthAuditLogger audit,
        ILoginAttemptProtector attempts,
        IOptions<JwtOptions> jwtOptions)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _refreshTokens = refreshTokens;
        _audit = audit;
        _attempts = attempts;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<ApiResult<RegisterResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateRegister(request);
        if (errors.Count > 0)
            return ApiResult<RegisterResponse>.Fail(errors[0], 400, errors);

        var planName = string.IsNullOrWhiteSpace(request.Plan) ? PlanNames.Starter : request.Plan.Trim();
        if (!string.Equals(planName, PlanNames.Starter, StringComparison.OrdinalIgnoreCase))
            return ApiResult<RegisterResponse>.Fail("Subscription plan is not currently available.", 400);

        if (request.InstallationId == Guid.Empty)
            return ApiResult<RegisterResponse>.Fail("InstallationId is required.", 400);

        if (string.IsNullOrWhiteSpace(request.DeviceId))
            return ApiResult<RegisterResponse>.Fail("DeviceId is required.", 400);

        var existingInstallation = await _db.ClientInstallations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.InstallationId == request.InstallationId && !x.IsDeleted, cancellationToken);
        if (existingInstallation is not null)
        {
            // Idempotent retry of the same offline registration sync.
            if (request.ClientUserId is Guid clientUserId
                && clientUserId != Guid.Empty
                && existingInstallation.UserId == clientUserId)
            {
                var sub = await _db.Subscriptions.AsNoTracking()
                    .Where(s => s.CompanyId == existingInstallation.CompanyId && !s.IsDeleted)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                var existingPlan = sub is null
                    ? null
                    : await _db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == sub.PlanId, cancellationToken);
                return ApiResult<RegisterResponse>.Ok(new RegisterResponse
                {
                    Success = true,
                    UserId = existingInstallation.UserId,
                    CompanyId = existingInstallation.CompanyId,
                    SubscriptionId = sub?.Id ?? Guid.Empty,
                    Plan = existingPlan?.Name ?? PlanNames.Starter,
                    TrialStartDate = sub?.StartDate ?? DateTime.UtcNow,
                    TrialEndDate = sub?.EndDate ?? DateTime.UtcNow
                });
            }

            return ApiResult<RegisterResponse>.Fail(
                "This installation is already registered. Please sign in instead.", 409);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var mobile = NormalizeMobile(request.MobileNumber);

        if (request.ClientUserId is Guid existingUserId && existingUserId != Guid.Empty)
        {
            var existingUser = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == existingUserId && !u.IsDeleted, cancellationToken);
            if (existingUser is not null)
            {
                // Same stable user id already on server — treat as successful idempotent create.
                var membership = await _db.UserCompanies.AsNoTracking()
                    .FirstOrDefaultAsync(uc => uc.UserId == existingUser.Id && uc.IsActive && !uc.IsDeleted, cancellationToken);
                var sub = membership is null
                    ? null
                    : await _db.Subscriptions.AsNoTracking()
                        .Where(s => s.CompanyId == membership.CompanyId && !s.IsDeleted)
                        .OrderByDescending(s => s.CreatedAt)
                        .FirstOrDefaultAsync(cancellationToken);
                return ApiResult<RegisterResponse>.Ok(new RegisterResponse
                {
                    Success = true,
                    UserId = existingUser.Id,
                    CompanyId = membership?.CompanyId ?? Guid.Empty,
                    SubscriptionId = sub?.Id ?? Guid.Empty,
                    Plan = PlanNames.Starter,
                    TrialStartDate = sub?.StartDate ?? DateTime.UtcNow,
                    TrialEndDate = sub?.EndDate ?? DateTime.UtcNow
                });
            }
        }

        if (await _db.Users.AnyAsync(u => u.Email == email && !u.IsDeleted, cancellationToken))
            return ApiResult<RegisterResponse>.Fail("Email is already registered.", 409);

        if (await _db.Users.AnyAsync(u => u.MobileNumber == mobile && !u.IsDeleted, cancellationToken))
            return ApiResult<RegisterResponse>.Fail("Mobile number is already registered.", 409);

        var plan = await _db.Plans.FirstOrDefaultAsync(
            p => p.Name == PlanNames.Starter && p.IsActive, cancellationToken);
        if (plan is null || !plan.IsAvailable)
            return ApiResult<RegisterResponse>.Fail("Subscription plan is not currently available.", 400);

        var ownerRole = await _db.Roles.FirstOrDefaultAsync(
            r => r.Name == RoleNames.CompanyOwner && !r.IsDeleted, cancellationToken);
        if (ownerRole is null)
            return ApiResult<RegisterResponse>.Fail("Initial role is not configured.", 500);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var userId = request.ClientUserId is Guid uid && uid != Guid.Empty ? uid : Guid.NewGuid();
            var companyId = request.ClientCompanyId is Guid cid && cid != Guid.Empty ? cid : Guid.NewGuid();
            var membershipId = request.ClientUserCompanyId is Guid ucid && ucid != Guid.Empty ? ucid : Guid.NewGuid();

            var user = new User
            {
                Id = userId,
                UserCode = $"U{now:yyyyMMddHHmmss}{Random.Shared.Next(100, 999)}",
                FullName = request.FullName.Trim(),
                Email = email,
                MobileNumber = mobile,
                PasswordHash = _passwordHasher.HashPassword(request.Password),
                IsActiveUser = true,
                CreatedAt = now,
                SyncStatus = RecordSyncStatus.Pending
            };

            var company = new Company
            {
                Id = companyId,
                CompanyCode = $"C{now:yyyyMMddHHmmss}{Random.Shared.Next(100, 999)}",
                CompanyName = request.CompanyName.Trim(),
                OwnerName = string.IsNullOrWhiteSpace(request.OwnerName) ? user.FullName : request.OwnerName.Trim(),
                Email = request.CompanyEmail.Trim().ToLowerInvariant(),
                MobileNumber = NormalizeMobile(request.CompanyMobile),
                GSTNumber = request.GstNumber?.Trim() ?? string.Empty,
                PANNumber = request.PanNumber?.Trim() ?? string.Empty,
                Address = request.CompanyAddress?.Trim() ?? string.Empty,
                City = request.CompanyCity?.Trim() ?? string.Empty,
                State = request.CompanyState?.Trim() ?? string.Empty,
                Country = request.CompanyCountry?.Trim() ?? string.Empty,
                PinCode = request.CompanyPinCode?.Trim() ?? string.Empty,
                IsActive = true,
                CreatedAt = now,
                SyncStatus = RecordSyncStatus.Pending
            };

            var membership = new UserCompany
            {
                Id = membershipId,
                UserId = user.Id,
                CompanyId = company.Id,
                IsOwner = true,
                IsDefault = true,
                IsActive = true,
                CreatedAt = now,
                SyncStatus = RecordSyncStatus.Pending
            };

            var subscription = Subscription.StartFromPlan(company.Id, plan, now, isTrial: true);
            if (request.ClientSubscriptionId is Guid sid && sid != Guid.Empty)
                subscription.Id = sid;

            var userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = ownerRole.Id,
                CompanyId = company.Id,
                CreatedAt = now,
                SyncStatus = RecordSyncStatus.Pending
            };

            var clientInstallation = new ClientInstallation
            {
                Id = Guid.NewGuid(),
                InstallationId = request.InstallationId,
                DeviceId = request.DeviceId.Trim(),
                UserId = user.Id,
                CompanyId = company.Id,
                RegisteredAt = now,
                CreatedAt = now,
                SyncStatus = RecordSyncStatus.Pending
            };

            _db.Users.Add(user);
            _db.Companies.Add(company);
            _db.UserCompanies.Add(membership);
            _db.Subscriptions.Add(subscription);
            _db.UserRoles.Add(userRole);
            _db.ClientInstallations.Add(clientInstallation);
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return ApiResult<RegisterResponse>.Ok(new RegisterResponse
            {
                Success = true,
                UserId = user.Id,
                CompanyId = company.Id,
                SubscriptionId = subscription.Id,
                Plan = plan.Name,
                TrialStartDate = subscription.StartDate,
                TrialEndDate = subscription.EndDate
            });
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ApiResult<LoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return ApiResult<LoginResponse>.Fail("Invalid email or password.", 401);

        if (string.IsNullOrWhiteSpace(request.DeviceId))
            return ApiResult<LoginResponse>.Fail("DeviceId is required.", 400);

        var email = request.Email.Trim().ToLowerInvariant();
        if (_attempts.IsBlocked(email))
            return ApiResult<LoginResponse>.Fail("Too many failed login attempts. Please try again later.", 429);

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, cancellationToken);

        if (user is null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash) || !user.IsActiveUser)
        {
            _attempts.RecordFailure(email);
            _audit.LoginFailure(email, "invalid_credentials");
            return ApiResult<LoginResponse>.Fail("Invalid email or password.", 401);
        }

        var memberships = await _db.UserCompanies
            .AsNoTracking()
            .Include(uc => uc.Company)
            .Where(uc => uc.UserId == user.Id && uc.IsActive && !uc.IsDeleted)
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            _attempts.RecordFailure(email);
            _audit.LoginFailure(email, "no_company");
            return ApiResult<LoginResponse>.Fail("Invalid email or password.", 401);
        }

        UserCompany membership;
        if (request.CompanyId is Guid requestedCompanyId)
        {
            membership = memberships.FirstOrDefault(m => m.CompanyId == requestedCompanyId)!;
            if (membership is null)
            {
                _attempts.RecordFailure(email);
                _audit.LoginFailure(email, "unauthorized_company");
                return ApiResult<LoginResponse>.Fail("You do not have access to the selected company.", 403);
            }
        }
        else
        {
            membership = memberships.FirstOrDefault(m => m.IsDefault) ?? memberships[0];
        }

        if (membership.Company is null || !membership.Company.IsActive || membership.Company.IsDeleted)
            return ApiResult<LoginResponse>.Fail("Invalid email or password.", 401);

        var (roles, permissions) = await LoadRolesAndPermissionsAsync(user.Id, membership.CompanyId, cancellationToken);
        var response = await CreateSessionAndTokensAsync(
            user,
            membership,
            memberships,
            roles,
            permissions,
            request.DeviceId.Trim(),
            request.DeviceName?.Trim() ?? string.Empty,
            request.Platform?.Trim() ?? string.Empty,
            cancellationToken);

        _attempts.Reset(email);
        _audit.LoginSuccess(user.Id, membership.CompanyId, response.Session.SessionId, request.DeviceId);
        return ApiResult<LoginResponse>.Ok(response);
    }

    public async Task<ApiResult<LoginResponse>> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return ApiResult<LoginResponse>.Fail("Unauthorized.", 401);

        var hash = _refreshTokens.HashRefreshToken(request.RefreshToken);

        // Reuse detection: revoked session with same family that still matches this token.
        var revokedMatch = await _db.DeviceSessions
            .FirstOrDefaultAsync(s =>
                s.RefreshTokenHash == hash &&
                (!s.IsActive || s.RevokedAt != null) &&
                !s.IsDeleted, cancellationToken);

        if (revokedMatch is not null)
        {
            await RevokeFamilyAsync(revokedMatch.TokenFamilyId, revokedMatch.UserId, "refresh_reuse", cancellationToken);
            return ApiResult<LoginResponse>.Fail("Unauthorized.", 401);
        }

        var session = await _db.DeviceSessions
            .FirstOrDefaultAsync(s =>
                s.RefreshTokenHash == hash &&
                s.IsActive &&
                s.RevokedAt == null &&
                !s.IsDeleted, cancellationToken);

        if (session is null)
            return ApiResult<LoginResponse>.Fail("Unauthorized.", 401);

        if (request.SessionId is Guid sid && sid != session.Id)
            return ApiResult<LoginResponse>.Fail("Unauthorized.", 401);

        if (!string.IsNullOrWhiteSpace(request.DeviceId) &&
            !string.Equals(request.DeviceId, session.DeviceId, StringComparison.Ordinal))
            return ApiResult<LoginResponse>.Fail("Unauthorized.", 401);

        if (session.ExpiresAt <= DateTime.UtcNow)
        {
            session.IsActive = false;
            session.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return ApiResult<LoginResponse>.Fail("Unauthorized.", 401);
        }

        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Id == session.UserId && !u.IsDeleted, cancellationToken);
        if (user is null || !user.IsActiveUser)
            return ApiResult<LoginResponse>.Fail("Unauthorized.", 401);

        var membership = await _db.UserCompanies
            .Include(uc => uc.Company)
            .FirstOrDefaultAsync(uc =>
                uc.UserId == user.Id &&
                uc.CompanyId == session.CompanyId &&
                uc.IsActive &&
                !uc.IsDeleted, cancellationToken);

        if (membership?.Company is null || !membership.Company.IsActive)
            return ApiResult<LoginResponse>.Fail("Unauthorized.", 401);

        var memberships = await _db.UserCompanies
            .AsNoTracking()
            .Include(uc => uc.Company)
            .Where(uc => uc.UserId == user.Id && uc.IsActive && !uc.IsDeleted)
            .ToListAsync(cancellationToken);

        var (roles, permissions) = await LoadRolesAndPermissionsAsync(user.Id, membership.CompanyId, cancellationToken);

        // Rotate refresh token
        var newRefresh = _refreshTokens.GenerateRefreshToken();
        session.RefreshTokenHash = _refreshTokens.HashRefreshToken(newRefresh);
        session.LastUsedAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var (accessToken, expiresAt, _) = _jwt.CreateAccessToken(user.Id, membership.CompanyId, session.Id, roles);
        var response = BuildLoginResponse(
            user, membership, memberships, roles, permissions, session, accessToken, newRefresh, expiresAt);

        _audit.TokenRefresh(user.Id, session.Id);
        return ApiResult<LoginResponse>.Ok(response);
    }

    public async Task<ApiResult> LogoutAsync(
        ClaimsPrincipal principal,
        LogoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = TryGetUserId(principal);
        if (userId is null)
            return ApiResult.Fail("Unauthorized.", 401);

        DeviceSession? session = null;
        if (request.SessionId is Guid sid)
        {
            session = await _db.DeviceSessions.FirstOrDefaultAsync(
                s => s.Id == sid && s.UserId == userId && !s.IsDeleted, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hash = _refreshTokens.HashRefreshToken(request.RefreshToken);
            session = await _db.DeviceSessions.FirstOrDefaultAsync(
                s => s.RefreshTokenHash == hash && s.UserId == userId && !s.IsDeleted, cancellationToken);
        }
        else
        {
            var claimSession = TryGetSessionId(principal);
            if (claimSession is Guid csid)
            {
                session = await _db.DeviceSessions.FirstOrDefaultAsync(
                    s => s.Id == csid && s.UserId == userId && !s.IsDeleted, cancellationToken);
            }
        }

        if (session is null)
            return ApiResult.Ok();

        session.IsActive = false;
        session.RevokedAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        _audit.Logout(userId.Value, session.Id);
        return ApiResult.Ok();
    }

    public async Task<ApiResult<MeResponse>> GetMeAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var userId = TryGetUserId(principal);
        var companyId = TryGetCompanyId(principal);
        if (userId is null || companyId is null)
            return ApiResult<MeResponse>.Fail("Unauthorized.", 401);

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted && u.IsActiveUser, cancellationToken);
        if (user is null)
            return ApiResult<MeResponse>.Fail("Unauthorized.", 401);

        var memberships = await _db.UserCompanies.AsNoTracking()
            .Include(uc => uc.Company)
            .Where(uc => uc.UserId == user.Id && uc.IsActive && !uc.IsDeleted)
            .ToListAsync(cancellationToken);

        var current = memberships.FirstOrDefault(m => m.CompanyId == companyId);
        if (current?.Company is null)
            return ApiResult<MeResponse>.Fail("Unauthorized.", 401);

        var (roles, permissions) = await LoadRolesAndPermissionsAsync(user.Id, current.CompanyId, cancellationToken);

        return ApiResult<MeResponse>.Ok(new MeResponse
        {
            User = MapUser(user),
            CurrentCompany = MapCompany(current),
            Companies = memberships.Select(MapCompany).ToList(),
            Roles = roles,
            Permissions = permissions
        });
    }

    private async Task<LoginResponse> CreateSessionAndTokensAsync(
        User user,
        UserCompany membership,
        IReadOnlyList<UserCompany> memberships,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions,
        string deviceId,
        string deviceName,
        string platform,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var refresh = _refreshTokens.GenerateRefreshToken();
        var familyId = Guid.NewGuid().ToString("N");

        var existing = await _db.DeviceSessions
            .Where(s => s.UserId == user.Id && s.DeviceId == deviceId && s.IsActive && s.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var old in existing)
        {
            old.IsActive = false;
            old.RevokedAt = now;
            old.UpdatedAt = now;
        }

        var session = new DeviceSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CompanyId = membership.CompanyId,
            DeviceId = deviceId,
            DeviceName = deviceName,
            Platform = platform,
            RefreshTokenHash = _refreshTokens.HashRefreshToken(refresh),
            CreatedAt = now,
            LastUsedAt = now,
            ExpiresAt = now.AddDays(Math.Max(1, _jwtOptions.RefreshTokenDays)),
            IsActive = true,
            TokenFamilyId = familyId,
            SyncStatus = RecordSyncStatus.Pending
        };

        _db.DeviceSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        var (accessToken, expiresAt, _) = _jwt.CreateAccessToken(user.Id, membership.CompanyId, session.Id, roles);
        return BuildLoginResponse(user, membership, memberships, roles, permissions, session, accessToken, refresh, expiresAt);
    }

    private LoginResponse BuildLoginResponse(
        User user,
        UserCompany membership,
        IReadOnlyList<UserCompany> memberships,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions,
        DeviceSession session,
        string accessToken,
        string refreshToken,
        DateTime accessExpiresAt)
    {
        var offlineMax = OfflineSessionDefaults.OfflineSessionMaxAge;
        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _jwt.AccessTokenLifetimeSeconds,
            AccessTokenExpiresAtUtc = accessExpiresAt,
            User = MapUser(user),
            CurrentCompany = MapCompany(membership),
            Companies = memberships.Select(MapCompany).ToList(),
            Roles = roles,
            Permissions = permissions,
            Session = new AuthSessionDto
            {
                SessionId = session.Id,
                UserId = session.UserId,
                CompanyId = session.CompanyId,
                DeviceId = session.DeviceId,
                DeviceName = session.DeviceName,
                Platform = session.Platform,
                CreatedAtUtc = session.CreatedAt,
                ExpiresAtUtc = session.ExpiresAt,
                OfflineSessionExpiresAtUtc = session.CreatedAt.Add(offlineMax)
            }
        };
    }

    private async Task<(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)> LoadRolesAndPermissionsAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var roleIds = await _db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId && !ur.IsDeleted &&
                         (ur.CompanyId == null || ur.CompanyId == companyId))
            .Select(ur => ur.RoleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var roles = await _db.Roles.AsNoTracking()
            .Where(r => roleIds.Contains(r.Id) && !r.IsDeleted)
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        var permissions = await _db.RolePermissions.AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId) && !rp.IsDeleted)
            .Join(_db.Permissions.AsNoTracking().Where(p => !p.IsDeleted),
                rp => rp.PermissionId,
                p => p.Id,
                (_, p) => p.PermissionName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        return (roles, permissions);
    }

    private async Task RevokeFamilyAsync(string? familyId, Guid userId, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(familyId)) return;
        var sessions = await _db.DeviceSessions
            .Where(s => s.UserId == userId && s.TokenFamilyId == familyId && s.IsActive)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var s in sessions)
        {
            s.IsActive = false;
            s.RevokedAt = now;
            s.UpdatedAt = now;
            _audit.SessionRevoked(userId, s.Id, reason);
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static List<string> ValidateRegister(RegisterRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.FullName)) errors.Add("Full name is required.");
        if (string.IsNullOrWhiteSpace(request.Email)) errors.Add("Email is required.");
        if (string.IsNullOrWhiteSpace(request.MobileNumber)) errors.Add("Mobile number is required.");
        if (string.IsNullOrWhiteSpace(request.Password)) errors.Add("Password is required.");
        if (string.IsNullOrWhiteSpace(request.ConfirmPassword)) errors.Add("Confirm password is required.");
        if (!string.IsNullOrWhiteSpace(request.Password) &&
            !string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            errors.Add("Passwords do not match.");
        if (string.IsNullOrWhiteSpace(request.CompanyName)) errors.Add("Company name is required.");
        if (string.IsNullOrWhiteSpace(request.CompanyEmail)) errors.Add("Email is required.");
        if (string.IsNullOrWhiteSpace(request.CompanyMobile)) errors.Add("Mobile number is required.");
        return errors;
    }

    private static string NormalizeMobile(string? mobile)
        => string.IsNullOrWhiteSpace(mobile)
            ? string.Empty
            : new string(mobile.Where(char.IsDigit).ToArray());

    private static AuthUserDto MapUser(User user) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        Email = user.Email,
        MobileNumber = user.MobileNumber,
        IsActive = user.IsActiveUser
    };

    private static AuthCompanyDto MapCompany(UserCompany membership) => new()
    {
        Id = membership.CompanyId,
        CompanyName = membership.Company?.CompanyName ?? string.Empty,
        Email = membership.Company?.Email ?? string.Empty,
        MobileNumber = membership.Company?.MobileNumber ?? string.Empty,
        IsDefault = membership.IsDefault,
        IsOwner = membership.IsOwner
    };

    private static Guid? TryGetUserId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static Guid? TryGetCompanyId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(JwtTokenService.CompanyIdClaim);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static Guid? TryGetSessionId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(JwtTokenService.SessionIdClaim);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
