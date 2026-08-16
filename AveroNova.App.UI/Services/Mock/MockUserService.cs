using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockUserService : IUserService
{
    public Task<List<UserModel>> GetAllAsync(Guid companyId)
        => Task.FromResult(MockDataStore.Users.Where(u => u.CompanyId == companyId).ToList());

    public Task<UserModel?> GetByIdAsync(Guid id)
        => Task.FromResult(MockDataStore.Users.FirstOrDefault(u => u.LocalId == id));

    public Task<(bool Ok, string? Error)> CreateAsync(UserModel user)
    {
        user.LocalId    = Guid.NewGuid();
        user.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Users.Add(user);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> UpdateAsync(UserModel user)
    {
        var idx = MockDataStore.Users.FindIndex(u => u.LocalId == user.LocalId);
        if (idx < 0) return Task.FromResult((false, "User not found."));
        user.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Users[idx] = user;
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        var item = MockDataStore.Users.FirstOrDefault(u => u.LocalId == id);
        if (item == null) return Task.FromResult((false, "User not found."));
        MockDataStore.Users.Remove(item);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> ActivateAsync(Guid id)
    {
        var user = MockDataStore.Users.FirstOrDefault(u => u.LocalId == id);
        if (user == null) return Task.FromResult((false, "User not found."));
        user.Status     = UserStatus.Active;
        user.SyncStatus = SyncStatus.PendingSync;
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> DeactivateAsync(Guid id)
    {
        var user = MockDataStore.Users.FirstOrDefault(u => u.LocalId == id);
        if (user == null) return Task.FromResult((false, "User not found."));
        user.Status     = UserStatus.Inactive;
        user.SyncStatus = SyncStatus.PendingSync;
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> ResetPasswordAsync(Guid id)
        => Task.FromResult<(bool, string?)>((true, null));

    public Task<List<RoleModel>> GetRolesAsync(Guid companyId)
        => Task.FromResult(MockDataStore.Roles.Where(r => r.CompanyId == companyId).ToList());

    public Task<RoleModel?> GetRoleByIdAsync(Guid id)
        => Task.FromResult(MockDataStore.Roles.FirstOrDefault(r => r.LocalId == id));

    public Task<(bool Ok, string? Error)> CreateRoleAsync(RoleModel role)
    {
        role.LocalId    = Guid.NewGuid();
        role.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Roles.Add(role);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<List<RoleModel>> GetAllRolesAsync()
    {
        var roles = new List<RoleModel>
    {
        new RoleModel
        {
            LocalId = Guid.NewGuid(),
            Name = "Admin"
        },
        new RoleModel
        {
            LocalId = Guid.NewGuid(),
            Name = "Manager"
        },
        new RoleModel
        {
            LocalId = Guid.NewGuid(),
            Name = "User"
        }
    };

        return Task.FromResult(roles);
    }
    public Task<(bool Ok, string? Error)> UpdateRoleAsync(RoleModel role)
    {
        var idx = MockDataStore.Roles.FindIndex(r => r.LocalId == role.LocalId);
        if (idx < 0) return Task.FromResult((false, "Role not found."));
        role.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Roles[idx] = role;
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> DeleteRoleAsync(Guid id)
    {
        var item = MockDataStore.Roles.FirstOrDefault(r => r.LocalId == id);
        if (item == null) return Task.FromResult((false, "Role not found."));
        if (item.IsSystem) return Task.FromResult((false, "Cannot delete a system role."));
        MockDataStore.Roles.Remove(item);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<List<PermissionModel>> GetPermissionsAsync() => Task.FromResult(new List<PermissionModel>
    {
        new() { Key = "dashboard.view",   Module = "Dashboard",  Label = "View Dashboard",      IsGranted = true },
        new() { Key = "billing.view",     Module = "Billing",    Label = "View Invoices",        IsGranted = true },
        new() { Key = "billing.create",   Module = "Billing",    Label = "Create Invoices",      IsGranted = true },
        new() { Key = "billing.delete",   Module = "Billing",    Label = "Delete Invoices",      IsGranted = false },
        new() { Key = "customers.view",   Module = "Customers",  Label = "View Customers",       IsGranted = true },
        new() { Key = "customers.manage", Module = "Customers",  Label = "Manage Customers",     IsGranted = true },
        new() { Key = "products.view",    Module = "Products",   Label = "View Products",        IsGranted = true },
        new() { Key = "products.manage",  Module = "Products",   Label = "Manage Products",      IsGranted = false },
        new() { Key = "inventory.view",   Module = "Inventory",  Label = "View Inventory",       IsGranted = true },
        new() { Key = "inventory.manage", Module = "Inventory",  Label = "Manage Inventory",     IsGranted = false },
        new() { Key = "payments.view",    Module = "Payments",   Label = "View Payments",        IsGranted = true },
        new() { Key = "payments.manage",  Module = "Payments",   Label = "Manage Payments",      IsGranted = false },
        new() { Key = "reports.view",     Module = "Reports",    Label = "View Reports",         IsGranted = true },
        new() { Key = "users.manage",     Module = "Admin",      Label = "Manage Users",         IsGranted = false },
        new() { Key = "settings.manage",  Module = "Settings",   Label = "Manage Settings",      IsGranted = false },
    });
}
