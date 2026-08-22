using AveroNova.Application.Navigation;
using AveroNova.App.UI.Navigation;
using AveroNova.Domain.Constants;

namespace AveroNova.App.UI.SubscriptionAccess;

/// <summary>
/// Maps routes to the subscription module plus the specific permission
/// required for that feature. Used so hiding a sidebar item is not the security boundary.
/// </summary>
public static class FeatureRouteMap
{
    private static readonly Dictionary<string, (string Module, string Permission)> Routes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [AppRoutes.Dashboard] = (SubscriptionModules.Dashboard, PermissionNames.DashboardView),
            [AppRoutes.Company] = (SubscriptionModules.Company, PermissionNames.CompanyView),
            [AppRoutes.CompanyAdd] = (SubscriptionModules.Company, PermissionNames.CompanyUpdate),
            [AppRoutes.CompanyEdit] = (SubscriptionModules.Company, PermissionNames.CompanyUpdate),
            [AppRoutes.Customers] = (SubscriptionModules.Customers, PermissionNames.CustomersView),
            [AppRoutes.CustomerAdd] = (SubscriptionModules.Customers, PermissionNames.CustomersManage),
            [AppRoutes.CustomerEdit] = (SubscriptionModules.Customers, PermissionNames.CustomersManage),
            [AppRoutes.CustomerView] = (SubscriptionModules.Customers, PermissionNames.CustomersView),
            [AppRoutes.Products] = (SubscriptionModules.Products, PermissionNames.ProductsView),
            [AppRoutes.ProductAdd] = (SubscriptionModules.Products, PermissionNames.ProductsManage),
            [AppRoutes.ProductEdit] = (SubscriptionModules.Products, PermissionNames.ProductsManage),
            [AppRoutes.ProductView] = (SubscriptionModules.Products, PermissionNames.ProductsView),
            [AppRoutes.Inventory] = (SubscriptionModules.Inventory, PermissionNames.InventoryView),
            [AppRoutes.StockAdjust] = (SubscriptionModules.Inventory, PermissionNames.InventoryView),
            [AppRoutes.StockMovement] = (SubscriptionModules.Inventory, PermissionNames.InventoryView),
            [AppRoutes.Billing] = (SubscriptionModules.Sales, PermissionNames.BillingView),
            [AppRoutes.InvoiceNew] = (SubscriptionModules.Sales, PermissionNames.BillingView),
            [AppRoutes.InvoiceView] = (SubscriptionModules.Sales, PermissionNames.BillingView),
            [AppRoutes.InvoiceEdit] = (SubscriptionModules.Sales, PermissionNames.BillingView),
            [AppRoutes.SalesReturns] = (SubscriptionModules.Sales, PermissionNames.BillingView),
            [AppRoutes.SalesReturnNew] = (SubscriptionModules.Sales, PermissionNames.BillingView),
            [AppRoutes.Purchases] = (SubscriptionModules.Purchase, PermissionNames.PurchasesView),
            [AppRoutes.PurchaseNew] = (SubscriptionModules.Purchase, PermissionNames.PurchasesView),
            [AppRoutes.PurchaseView] = (SubscriptionModules.Purchase, PermissionNames.PurchasesView),
            [AppRoutes.PurchaseReturns] = (SubscriptionModules.Purchase, PermissionNames.PurchasesView),
            [AppRoutes.PurchaseReturnNew] = (SubscriptionModules.Purchase, PermissionNames.PurchasesView),
            [AppRoutes.Payments] = (SubscriptionModules.Payments, PermissionNames.PaymentsView),
            [AppRoutes.PaymentAdd] = (SubscriptionModules.Payments, PermissionNames.PaymentsView),
            [AppRoutes.PaymentView] = (SubscriptionModules.Payments, PermissionNames.PaymentsView),
            [AppRoutes.Reports] = (SubscriptionModules.Reports, PermissionNames.ReportsView),
            [AppRoutes.Users] = (SubscriptionModules.Settings, PermissionNames.UsersView),
            [AppRoutes.UserAdd] = (SubscriptionModules.Settings, PermissionNames.UsersCreate),
            [AppRoutes.UserView] = (SubscriptionModules.Settings, PermissionNames.UsersView),
            [AppRoutes.UserEdit] = (SubscriptionModules.Settings, PermissionNames.UsersUpdate),
            [AppRoutes.Roles] = (SubscriptionModules.Settings, PermissionNames.UsersView),
            [AppRoutes.RoleAdd] = (SubscriptionModules.Settings, PermissionNames.UsersUpdate),
            [AppRoutes.RoleEdit] = (SubscriptionModules.Settings, PermissionNames.UsersUpdate),
            [AppRoutes.Permissions] = (SubscriptionModules.Settings, PermissionNames.UsersView),
            [AppRoutes.UsersRoles] = (SubscriptionModules.Settings, PermissionNames.UsersView),
            [AppRoutes.Settings] = (SubscriptionModules.Settings, PermissionNames.SettingsManage),
            [AppRoutes.Expenses] = (SubscriptionModules.Expenses, PermissionNames.ExpensesView),
            [AppRoutes.ExpenseAdd] = (SubscriptionModules.Expenses, PermissionNames.ExpensesView),
            [AppRoutes.ExpenseView] = (SubscriptionModules.Expenses, PermissionNames.ExpensesView),
            [AppRoutes.ExpenseEdit] = (SubscriptionModules.Expenses, PermissionNames.ExpensesView)
        };

    public static bool TryResolve(string? location, out string moduleKey, out string permissionName)
    {
        moduleKey = string.Empty;
        permissionName = string.Empty;
        if (string.IsNullOrWhiteSpace(location))
            return false;

        var trimmed = location.Trim().Trim('/');
        foreach (var pair in Routes)
        {
            if (trimmed.Equals(pair.Key, StringComparison.OrdinalIgnoreCase)
                || trimmed.EndsWith("/" + pair.Key, StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains(pair.Key + "?", StringComparison.OrdinalIgnoreCase))
            {
                moduleKey = pair.Value.Module;
                permissionName = pair.Value.Permission;
                return true;
            }
        }

        var menu = NavigationMenuCatalog.Find(trimmed);
        if (menu == null)
            return false;

        moduleKey = menu.SubscriptionModule;
        permissionName = menu.PermissionName;
        return true;
    }
}
