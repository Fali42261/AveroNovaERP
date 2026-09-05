namespace AveroNova.App.UI.Navigation;

/// <summary>
/// Permission → menu mapping. Sidebar visibility is driven by these definitions, not role names.
/// </summary>
public sealed record MenuDefinition(
    string Key,
    string Title,
    string Group,
    string? RequiredPermission);

public static class MenuCatalog
{
    public static IReadOnlyList<MenuDefinition> Items { get; } =
    [
        new("Dashboard", "Dashboard", "DASHBOARD", "Dashboard.View"),
        new("Company", "Company", "BUSINESS", "Company.Manage"),
        new("Customers", "Customers", "BUSINESS", "Customers.View"),
        new("Products", "Products", "BUSINESS", "Inventory.View"),
        new("Inventory", "Inventory", "BUSINESS", "Inventory.View"),
        new("Billing", "Billing", "BUSINESS", "Sales.View"),
        new("Purchases", "Purchases", "BUSINESS", "Sales.View"),
        new("Payments", "Payments", "BUSINESS", "Sales.View"),
        new("SalesReturns", "Sales Returns", "BUSINESS", "Sales.View"),
        new("PurchaseReturns", "Purchase Returns", "BUSINESS", "Sales.View"),
        new("Expenses", "Expenses", "BUSINESS", "Reports.View"),
        new("Reports", "Reports", "REPORTS", "Reports.View"),
        new("Users", "Users", "ADMINISTRATION", "Users.Manage"),
        new("Roles", "Roles", "ADMINISTRATION", "Users.Manage"),
        new("Permissions", "Permissions", "ADMINISTRATION", "Users.Manage"),
        new("License", "License", "SUBSCRIPTION", null),
        new("Notifications", "Notifications", "SYSTEM", "Dashboard.View"),
        new("SyncCenter", "Sync Center", "SYSTEM", "Dashboard.View"),
        new("Settings", "Settings", "SYSTEM", "Company.Manage"),
        new("Help", "Help & Support", "SYSTEM", null),
        new("About", "About", "SYSTEM", null)
    ];

    public static bool IsAllowed(string key, IEnumerable<string> permissions)
    {
        var item = Items.FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
        if (item is null)
            return false;
        if (string.IsNullOrWhiteSpace(item.RequiredPermission))
            return true;

        return permissions.Contains(item.RequiredPermission, StringComparer.OrdinalIgnoreCase);
    }
}
