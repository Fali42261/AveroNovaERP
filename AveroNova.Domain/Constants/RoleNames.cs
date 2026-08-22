namespace AveroNova.Domain.Constants;

/// <summary>
/// Catalog role names stored in <c>Roles.Name</c>.
/// Owner is not a role; company ownership is <c>UserCompany.IsOwner</c>.
/// </summary>
public static class RoleNames
{
    public const string Administrator = "Administrator";
    public const string HR = "HR";
    public const string Manager = "Manager";
    public const string Sales = "Sales";
    public const string Inventory = "Inventory";
    public const string Accountant = "Accountant";
    public const string CustomRole = "Custom Role";

    public const string AdminDisplayName = "Admin";

    public static readonly IReadOnlyList<string> AssignableCatalog =
    [
        Administrator,
        HR,
        Manager,
        Sales,
        Inventory,
        Accountant,
        CustomRole
    ];

    public static bool IsAssignable(string? name)
        => !string.IsNullOrWhiteSpace(name)
           && AssignableCatalog.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool IsProtectedOwnerName(string? name)
        => string.Equals(name?.Trim(), "Owner", StringComparison.OrdinalIgnoreCase);

    public static string DisplayName(string? name)
    {
        if (string.Equals(name, Administrator, StringComparison.OrdinalIgnoreCase))
            return AdminDisplayName;
        return string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
    }
}
