using AveroNova.Domain.Constants;
using AveroNova.App.UI.Navigation;

namespace AveroNova.App.UI.SubscriptionAccess;

public static class SubscriptionRouteMap
{
    private static readonly Dictionary<string, string> RouteModules = new(StringComparer.OrdinalIgnoreCase)
    {
        [AppRoutes.Dashboard] = SubscriptionModules.Dashboard,
        [AppRoutes.Company] = SubscriptionModules.Company,
        [AppRoutes.CompanyAdd] = SubscriptionModules.Company,
        [AppRoutes.CompanyEdit] = SubscriptionModules.Company,
        [AppRoutes.Customers] = SubscriptionModules.Customers,
        [AppRoutes.CustomerAdd] = SubscriptionModules.Customers,
        [AppRoutes.CustomerEdit] = SubscriptionModules.Customers,
        [AppRoutes.CustomerView] = SubscriptionModules.Customers,
        [AppRoutes.Products] = SubscriptionModules.Products,
        [AppRoutes.ProductAdd] = SubscriptionModules.Products,
        [AppRoutes.ProductEdit] = SubscriptionModules.Products,
        [AppRoutes.ProductView] = SubscriptionModules.Products,
        [AppRoutes.Inventory] = SubscriptionModules.Inventory,
        [AppRoutes.StockAdjust] = SubscriptionModules.Inventory,
        [AppRoutes.StockMovement] = SubscriptionModules.Inventory,
        [AppRoutes.Billing] = SubscriptionModules.Sales,
        [AppRoutes.InvoiceNew] = SubscriptionModules.Sales,
        [AppRoutes.InvoiceView] = SubscriptionModules.Sales,
        [AppRoutes.InvoiceEdit] = SubscriptionModules.Sales,
        [AppRoutes.SalesReturns] = SubscriptionModules.Sales,
        [AppRoutes.SalesReturnNew] = SubscriptionModules.Sales,
        [AppRoutes.Purchases] = SubscriptionModules.Purchase,
        [AppRoutes.PurchaseNew] = SubscriptionModules.Purchase,
        [AppRoutes.PurchaseView] = SubscriptionModules.Purchase,
        [AppRoutes.PurchaseReturns] = SubscriptionModules.Purchase,
        [AppRoutes.PurchaseReturnNew] = SubscriptionModules.Purchase,
        [AppRoutes.Payments] = SubscriptionModules.Payments,
        [AppRoutes.PaymentAdd] = SubscriptionModules.Payments,
        [AppRoutes.PaymentView] = SubscriptionModules.Payments,
        [AppRoutes.Reports] = SubscriptionModules.Reports,
        [AppRoutes.Users] = SubscriptionModules.Settings,
        [AppRoutes.UserAdd] = SubscriptionModules.Settings,
        [AppRoutes.UserView] = SubscriptionModules.Settings,
        [AppRoutes.UserEdit] = SubscriptionModules.Settings,
        [AppRoutes.Roles] = SubscriptionModules.Settings,
        [AppRoutes.RoleAdd] = SubscriptionModules.Settings,
        [AppRoutes.RoleEdit] = SubscriptionModules.Settings,
        [AppRoutes.Permissions] = SubscriptionModules.Settings,
        [AppRoutes.UsersRoles] = SubscriptionModules.Settings,
        [AppRoutes.Settings] = SubscriptionModules.Settings,
        [AppRoutes.Expenses] = SubscriptionModules.Expenses,
        [AppRoutes.ExpenseAdd] = SubscriptionModules.Expenses,
        [AppRoutes.ExpenseView] = SubscriptionModules.Expenses,
        [AppRoutes.ExpenseEdit] = SubscriptionModules.Expenses
    };

    public static string? ModuleForLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return null;

        var trimmed = location.Trim().Trim('/');
        foreach (var pair in RouteModules)
        {
            if (trimmed.Equals(pair.Key, StringComparison.OrdinalIgnoreCase)
                || trimmed.EndsWith("/" + pair.Key, StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains(pair.Key + "?", StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }
}
