using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

public sealed class UserListQuery
{
    public string? SearchText { get; init; }
    public Guid? RoleId { get; init; }
    public UserStatus? Status { get; init; }
}

public interface IUserService
{
    Task<List<UserModel>> QueryAsync(UserListQuery query);
    Task<List<UserModel>> GetAllAsync(Guid companyId);
    Task<UserModel?> GetByIdAsync(Guid id);
    Task<(bool Ok, string? Error)> CreateAsync(UserModel user);
    Task<(bool Ok, string? Error)> UpdateAsync(UserModel user);
    Task<(bool Ok, string? Error)> DeleteAsync(Guid id);
    Task<(bool Ok, string? Error)> ActivateAsync(Guid id);
    Task<(bool Ok, string? Error)> DeactivateAsync(Guid id);
    Task<(bool Ok, string? Error)> ResetPasswordAsync(Guid id);

    Task<List<RoleModel>> GetRolesAsync(Guid companyId);
    Task<List<RoleModel>> GetAssignableRolesAsync();
    Task<RoleModel?> GetRoleByIdAsync(Guid id);
    Task<List<RoleModel>> GetAllRolesAsync();
    Task<(bool Ok, string? Error)> CreateRoleAsync(RoleModel role);
    Task<(bool Ok, string? Error)> UpdateRoleAsync(RoleModel role);
    Task<(bool Ok, string? Error)> DeleteRoleAsync(Guid id);
    Task<List<PermissionModel>> GetPermissionsAsync();
}
