using System.Collections.Generic;

namespace AveroNova.Domain.Entities;

public class Permission : BaseEntity
{
    public string PermissionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
