using System.Text.RegularExpressions;
using AveroNova.Application.DTOs;
using AveroNova.Application.Interfaces.Repositories;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;

namespace AveroNova.App.UI.Services.Local;

public sealed class LocalUserService : IUserService
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly ICompanyUserRepository _users;
    private readonly CurrentAccessService _access;

    public LocalUserService(ICompanyUserRepository users, CurrentAccessService access)
    {
        _users = users;
        _access = access;
    }

    public Task<List<UserModel>> GetAllAsync(Guid companyId)
        => QueryAsync(new UserListQuery());

    public async Task<List<UserModel>> QueryAsync(UserListQuery query)
    {
        var companyId = CurrentCompanyId();
        if (companyId == Guid.Empty || !await CanAsync(PermissionNames.UsersView))
            return [];

        var isActive = query.Status switch
        {
            UserStatus.Active => true,
            UserStatus.Inactive => false,
            _ => (bool?)null
        };

        var items = await _users.QueryAsync(companyId, query.SearchText, query.RoleId, isActive);
        return items.Select(Map).ToList();
    }

    public async Task<UserModel?> GetByIdAsync(Guid id)
    {
        var companyId = CurrentCompanyId();
        if (companyId == Guid.Empty || id == Guid.Empty || !await CanAsync(PermissionNames.UsersView))
            return null;

        var item = await _users.GetByIdAsync(companyId, id);
        return item == null ? null : Map(item);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(UserModel user)
    {
        var companyId = CurrentCompanyId();
        if (companyId == Guid.Empty)
            return (false, "A company context is required.");
        if (!await CanAsync(PermissionNames.UsersCreate))
            return (false, "You do not have permission to create users.");

        var error = Validate(user, requirePassword: true);
        if (error != null)
            return (false, error);

        if (user.RoleId is not Guid roleId || roleId == Guid.Empty)
            return (false, "Role is required.");
        if (!await CanAsync(PermissionNames.UsersAssignRole))
            return (false, "You do not have permission to assign roles.");
        if (!await _users.RoleIsAssignableAsync(roleId))
            return (false, "The selected role cannot be assigned.");

        var email = user.Email.Trim();
        if (await _users.EmailExistsAsync(email, null))
            return (false, "User email already exists.");

        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var entity = new User
        {
            Id = userId,
            UserCode = UniqueCode("U"),
            FullName = Clamp(user.Name, 150),
            Email = Clamp(email, 150),
            MobileNumber = Clamp(user.Phone, 15),
            PasswordHash = LocalPasswordHasher.Hash(user.Password!),
            IsActiveUser = user.Status != UserStatus.Inactive,
            CreatedAt = now,
            IsDeleted = false
        };

        var membership = new UserCompany
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CompanyId = companyId,
            IsOwner = false,
            IsActive = user.Status != UserStatus.Inactive,
            CreatedAt = now,
            IsDeleted = false
        };

        var assignment = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            CompanyId = companyId,
            CreatedAt = now,
            IsDeleted = false
        };

        try
        {
            await _users.CreateInCompanyAsync(entity, membership, assignment);
            user.LocalId = userId;
            user.CompanyId = companyId;
            user.Password = null;
            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] User create failed: {ex.Message}");
            return (false, "Unable to create user.");
        }
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(UserModel user)
    {
        var companyId = CurrentCompanyId();
        if (companyId == Guid.Empty || user.LocalId == Guid.Empty)
            return (false, "Unable to update user.");
        if (!await CanAsync(PermissionNames.UsersUpdate))
            return (false, "You do not have permission to update users.");

        var error = Validate(user, requirePassword: false);
        if (error != null)
            return (false, error);

        var existing = await _users.GetByIdAsync(companyId, user.LocalId);
        if (existing == null)
            return (false, "User not found.");
        if (existing.Membership.IsOwner || await _users.IsOwnerAsync(companyId, user.LocalId))
            return (false, "The company owner cannot be edited.");

        var email = user.Email.Trim();
        if (await _users.EmailExistsAsync(email, user.LocalId))
            return (false, "User email already exists.");

        var roleChanged = user.RoleId.HasValue && user.RoleId != existing.PrimaryRoleId;
        if (roleChanged)
        {
            if (!await CanAsync(PermissionNames.UsersAssignRole))
                return (false, "You do not have permission to assign roles.");
            if (!await _users.RoleIsAssignableAsync(user.RoleId!.Value))
                return (false, "The selected role cannot be assigned.");
        }

        var now = DateTime.UtcNow;
        var entity = existing.User;
        entity.FullName = Clamp(user.Name, 150);
        entity.Email = Clamp(email, 150);
        entity.MobileNumber = Clamp(user.Phone, 15);
        entity.UpdatedAt = now;

        try
        {
            await _users.UpdateInCompanyAsync(
                companyId,
                entity,
                roleChanged || existing.PrimaryRoleId == null ? user.RoleId : existing.PrimaryRoleId,
                user.Status != UserStatus.Inactive);
            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] User update failed: {ex.Message}");
            return (false, "Unable to update user.");
        }
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        var companyId = CurrentCompanyId();
        if (companyId == Guid.Empty || id == Guid.Empty)
            return (false, "Unable to delete user.");
        if (!await CanAsync(PermissionNames.UsersDelete))
            return (false, "You do not have permission to delete users.");
        if (LocalSessionStore.UserId == id)
            return (false, "You cannot delete your own account.");
        if (await _users.IsOwnerAsync(companyId, id))
            return (false, "The company owner cannot be deleted.");

        try
        {
            var deleted = await _users.SoftDeleteInCompanyAsync(companyId, id);
            return deleted ? (true, null) : (false, "User not found.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] User delete failed: {ex.Message}");
            return (false, "Unable to delete user.");
        }
    }

    public Task<(bool Ok, string? Error)> ActivateAsync(Guid id)
        => SetActiveAsync(id, true);

    public Task<(bool Ok, string? Error)> DeactivateAsync(Guid id)
        => SetActiveAsync(id, false);

    public Task<(bool Ok, string? Error)> ResetPasswordAsync(Guid id)
        => Task.FromResult<(bool, string?)>((false, "Set a password when creating the user."));

    public async Task<List<RoleModel>> GetRolesAsync(Guid companyId)
    {
        var current = CurrentCompanyId();
        if (current == Guid.Empty)
            return [];

        var roles = await _users.GetAssignableRolesAsync();
        var result = new List<RoleModel>();
        foreach (var role in roles)
        {
            var count = await _users.CountUsersWithRoleAsync(current, role.Id);
            result.Add(MapRole(role, current, count));
        }

        return result;
    }

    public async Task<List<RoleModel>> GetAssignableRolesAsync()
    {
        if (CurrentCompanyId() == Guid.Empty)
            return [];

        var roles = await _users.GetAssignableRolesAsync();
        return roles.Select(r => MapRole(r, CurrentCompanyId(), 0)).ToList();
    }

    public async Task<RoleModel?> GetRoleByIdAsync(Guid id)
    {
        var role = await _users.GetRoleByIdAsync(id);
        return role == null ? null : MapRole(role, CurrentCompanyId(), 0);
    }

    public Task<List<RoleModel>> GetAllRolesAsync()
        => GetAssignableRolesAsync();

    public Task<(bool Ok, string? Error)> CreateRoleAsync(RoleModel role)
        => Task.FromResult<(bool, string?)>((false, "Role editing is not available in this release."));

    public Task<(bool Ok, string? Error)> UpdateRoleAsync(RoleModel role)
        => Task.FromResult<(bool, string?)>((false, "Role editing is not available in this release."));

    public async Task<(bool Ok, string? Error)> DeleteRoleAsync(Guid id)
    {
        var companyId = CurrentCompanyId();
        if (companyId == Guid.Empty || id == Guid.Empty)
            return (false, "Unable to delete role.");
        if (!await CanAsync(PermissionNames.UsersManage))
            return (false, "You do not have permission to delete roles.");

        var role = await _users.GetRoleByIdAsync(id);
        if (role == null)
            return (false, "Role not found.");
        if (RoleNames.IsAssignable(role.Name) || RoleNames.IsProtectedOwnerName(role.Name))
            return (false, "Catalog roles cannot be deleted.");

        var assigned = await _users.CountUsersWithRoleAsync(companyId, id);
        if (assigned > 0)
            return (false, "Role cannot be deleted while users are assigned.");

        try
        {
            var deleted = await _users.SoftDeleteRoleAsync(id);
            return deleted ? (true, null) : (false, "Unable to delete role.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Role delete failed: {ex.Message}");
            return (false, "Unable to delete role.");
        }
    }

    public async Task<List<PermissionModel>> GetPermissionsAsync()
    {
        var snapshot = await _access.GetSnapshotAsync();
        return snapshot.Permissions.Select(name => new PermissionModel
        {
            Key = name,
            Label = name,
            IsGranted = true
        }).ToList();
    }

    private async Task<(bool Ok, string? Error)> SetActiveAsync(Guid id, bool active)
    {
        var current = await GetByIdAsync(id);
        if (current == null)
            return (false, "User not found.");
        if (current.IsOwner)
            return (false, "The company owner cannot be deactivated.");

        current.Status = active ? UserStatus.Active : UserStatus.Inactive;
        return await UpdateAsync(current);
    }

    private async Task<bool> CanAsync(string permission)
    {
        var snapshot = await _access.GetSnapshotAsync();
        return PermissionNames.Grants(snapshot.Permissions, permission);
    }

    private static Guid CurrentCompanyId()
        => LocalSessionStore.CompanyId ?? Guid.Empty;

    private static UserModel Map(CompanyUserListItem item)
    {
        var names = item.RoleNames.Select(RoleNames.DisplayName).ToList();
        var name = item.User.FullName;
        return new UserModel
        {
            LocalId = item.User.Id,
            Name = name,
            Email = item.User.Email,
            Phone = item.User.MobileNumber ?? string.Empty,
            Role = names.Count == 0 ? "—" : string.Join(", ", names),
            RoleNames = names,
            RoleId = item.PrimaryRoleId,
            AvatarInitials = Initials(name),
            CompanyId = item.Membership.CompanyId,
            Status = item.Membership.IsActive && item.User.IsActiveUser
                ? UserStatus.Active
                : UserStatus.Inactive,
            IsOwner = item.Membership.IsOwner,
            CreatedAt = item.User.CreatedAt,
            UpdatedAt = item.User.UpdatedAt ?? item.User.CreatedAt,
            IsDeleted = item.User.IsDeleted,
            SyncStatus = SyncStatus.PendingSync
        };
    }

    private static RoleModel MapRole(Role role, Guid companyId, int userCount)
        => new()
        {
            LocalId = role.Id,
            Name = RoleNames.DisplayName(role.Name),
            Description = role.Description ?? string.Empty,
            IsSystem = string.Equals(role.Name, RoleNames.Administrator, StringComparison.OrdinalIgnoreCase),
            UserCount = userCount,
            CompanyId = companyId,
            IsDeleted = role.IsDeleted
        };

    private static string? Validate(UserModel user, bool requirePassword)
    {
        if (string.IsNullOrWhiteSpace(user.Name))
            return "Full name is required.";
        if (string.IsNullOrWhiteSpace(user.Email))
            return "Email is required.";
        if (!EmailRegex.IsMatch(user.Email.Trim()))
            return "Please enter a valid email address.";
        if (string.IsNullOrWhiteSpace(user.Phone))
            return "Mobile number is required.";
        if (user.Phone.Trim().Length > 15)
            return "Mobile number must be 15 characters or fewer.";
        if (requirePassword)
        {
            if (string.IsNullOrWhiteSpace(user.Password))
                return "Password is required.";
        }

        return null;
    }

    private static string UniqueCode(string prefix)
        => prefix + Convert.ToHexString(Guid.NewGuid().ToByteArray())[..8];

    private static string Clamp(string? value, int maxLength)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
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
}
