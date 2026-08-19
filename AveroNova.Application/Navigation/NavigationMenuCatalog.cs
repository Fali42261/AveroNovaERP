using AveroNova.Domain.Constants;

namespace AveroNova.Application.Navigation;

/// <summary>
/// Application menu catalog mapped to existing Permission and Subscription module keys.
/// Visibility is never based on role names. Order is explicit (not query order).
/// There is no Menu table in the current schema; this catalog is the menu source.
/// </summary>
public static class NavigationMenuCatalog
{
    public const string Dashboard = "dashboard";
    public const string Business = "business";
    public const string Company = "company";
    public const string Customers = "customers";
    public const string Products = "products";
    public const string Inventory = "inventory";
    public const string Sales = "sales";
    public const string Purchase = "purchase";
    public const string Payments = "payments";
    public const string Reports = "reports";
    public const string Administration = "administration";
    public const string Settings = "settings";
    public const string UsersRoles = "users-roles";

    public const string GroupDashboard = "Dashboard";
    public const string GroupBusiness = "Business";
    public const string GroupReports = "Reports";
    public const string GroupAdministration = "Administration";
    public const string GroupSystem = "System";

    public static IReadOnlyList<NavigationMenuDefinition> All { get; } =
    [
        new()
        {
            Key = Dashboard,
            Label = "Dashboard",
            IconResourceKey = "IconDashboard",
            SubscriptionModule = SubscriptionModules.Dashboard,
            PermissionName = PermissionNames.DashboardView,
            SortOrder = 1,
            GroupLabel = GroupDashboard
        },
        new()
        {
            Key = Business,
            Label = "Business",
            IconResourceKey = "IconBusiness",
            SubscriptionModule = SubscriptionModules.Company,
            PermissionName = PermissionNames.CompanyView,
            SortOrder = 2,
            GroupLabel = GroupBusiness
        },
        new()
        {
            Key = Company,
            Label = "Company",
            IconResourceKey = "IconCompany",
            SubscriptionModule = SubscriptionModules.Company,
            PermissionName = PermissionNames.CompanyView,
            SortOrder = 3,
            GroupLabel = GroupBusiness
        },
        new()
        {
            Key = Customers,
            Label = "Customers",
            IconResourceKey = "IconCustomers",
            SubscriptionModule = SubscriptionModules.Customers,
            PermissionName = PermissionNames.CustomersView,
            SortOrder = 4,
            GroupLabel = GroupBusiness
        },
        new()
        {
            Key = Products,
            Label = "Products",
            IconResourceKey = "IconProducts",
            SubscriptionModule = SubscriptionModules.Products,
            PermissionName = PermissionNames.ProductsView,
            SortOrder = 5,
            GroupLabel = GroupBusiness
        },
        new()
        {
            Key = Inventory,
            Label = "Inventory",
            IconResourceKey = "IconInventory",
            SubscriptionModule = SubscriptionModules.Inventory,
            PermissionName = PermissionNames.InventoryView,
            SortOrder = 6,
            GroupLabel = GroupBusiness
        },
        new()
        {
            Key = Sales,
            Label = "Sales",
            IconResourceKey = "IconSales",
            SubscriptionModule = SubscriptionModules.Sales,
            PermissionName = PermissionNames.BillingView,
            SortOrder = 7,
            GroupLabel = GroupBusiness
        },
        new()
        {
            Key = Purchase,
            Label = "Purchase",
            IconResourceKey = "IconPurchases",
            SubscriptionModule = SubscriptionModules.Purchase,
            PermissionName = PermissionNames.PurchasesView,
            SortOrder = 8,
            GroupLabel = GroupBusiness
        },
        new()
        {
            Key = Payments,
            Label = "Payments",
            IconResourceKey = "IconPayments",
            SubscriptionModule = SubscriptionModules.Payments,
            PermissionName = PermissionNames.PaymentsView,
            SortOrder = 9,
            GroupLabel = GroupBusiness
        },
        new()
        {
            Key = Reports,
            Label = "Reports",
            IconResourceKey = "IconReports",
            SubscriptionModule = SubscriptionModules.Reports,
            PermissionName = PermissionNames.ReportsView,
            SortOrder = 10,
            GroupLabel = GroupReports
        },
        new()
        {
            Key = Administration,
            Label = "Administration",
            IconResourceKey = "IconAdministration",
            SubscriptionModule = SubscriptionModules.Settings,
            PermissionName = PermissionNames.UsersManage,
            SortOrder = 11,
            IsGroup = true,
            GroupLabel = GroupAdministration,
            Surface = NavigationSurface.HeaderAccount
        },
        new()
        {
            Key = UsersRoles,
            Label = "Users & Roles",
            IconResourceKey = "IconUsers",
            SubscriptionModule = SubscriptionModules.Settings,
            PermissionName = PermissionNames.UsersManage,
            SortOrder = 1,
            ParentKey = Administration,
            Surface = NavigationSurface.HeaderAccount
        },
        new()
        {
            Key = Settings,
            Label = "Settings",
            IconResourceKey = "IconSettings",
            SubscriptionModule = SubscriptionModules.Settings,
            PermissionName = PermissionNames.SettingsManage,
            SortOrder = 12,
            GroupLabel = GroupSystem,
            Surface = NavigationSurface.HeaderAccount
        }
    ];

    public static NavigationMenuDefinition? Find(string key)
        => All.FirstOrDefault(d => d.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Sidebar presentation of an already-authorized menu tree.
    /// HeaderAccount items remain in the full snapshot for the user menu.
    /// </summary>
    public static IReadOnlyList<NavigationMenuNode> SidebarOnly(IReadOnlyList<NavigationMenuNode> menus)
        => menus.Where(m => m.Surface == NavigationSurface.Sidebar).ToList();

    public static bool ContainsKey(IReadOnlyList<NavigationMenuNode> menus, string key)
    {
        foreach (var item in menus)
        {
            if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                return true;
            if (item.Children.Any(child => child.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    public static IReadOnlyList<NavigationMenuNode> Build(
        IReadOnlyCollection<string> permissions,
        IReadOnlyCollection<string> enabledModules)
    {
        var permissionSet = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
        var moduleSet = new HashSet<string>(enabledModules, StringComparer.OrdinalIgnoreCase);

        var nodes = new List<NavigationMenuNode>();
        foreach (var definition in All.Where(d => d.ParentKey == null).OrderBy(d => d.SortOrder))
        {
            if (definition.IsGroup)
            {
                var children = All
                    .Where(d => string.Equals(d.ParentKey, definition.Key, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(d => d.SortOrder)
                    .Where(child => IsAuthorized(child, permissionSet, moduleSet))
                    .Select(child => ToNode(child))
                    .ToList();

                // Parent groups are containers only. Hide the parent when no
                // authorized children exist — never show an empty Administration leaf.
                if (children.Count == 0)
                    continue;

                nodes.Add(ToNode(definition, children, accordion: true));
                continue;
            }

            if (IsAuthorized(definition, permissionSet, moduleSet))
                nodes.Add(ToNode(definition));
        }

        return nodes;
    }

    public static bool IsAuthorized(
        NavigationMenuDefinition definition,
        IReadOnlySet<string> permissions,
        IReadOnlySet<string> enabledModules)
    {
        if (!enabledModules.Contains(definition.SubscriptionModule))
            return false;

        return permissions.Contains(definition.PermissionName);
    }

    private static NavigationMenuNode ToNode(
        NavigationMenuDefinition definition,
        IReadOnlyList<NavigationMenuNode>? children = null,
        bool? accordion = null)
        => new()
        {
            Key = definition.Key,
            Label = definition.Label,
            IconResourceKey = definition.IconResourceKey,
            SubscriptionModule = definition.SubscriptionModule,
            PermissionName = definition.PermissionName,
            SortOrder = definition.SortOrder,
            IsGroup = accordion ?? definition.IsGroup,
            GroupLabel = definition.GroupLabel,
            Surface = definition.Surface,
            Children = children ?? []
        };
}
