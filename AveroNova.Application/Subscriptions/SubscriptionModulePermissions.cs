using AveroNova.Domain.Constants;

namespace AveroNova.Application.Subscriptions
{
    public static class SubscriptionModulePermissions
    {
        public static IReadOnlyList<string> RequiredAny(string moduleKey) => moduleKey switch
        {
            SubscriptionModules.Dashboard => [PermissionNames.DashboardView],
            SubscriptionModules.Company => [PermissionNames.CompanyView, PermissionNames.SettingsManage],
            SubscriptionModules.Customers => [PermissionNames.CustomersView, PermissionNames.CustomersManage],
            SubscriptionModules.Products => [PermissionNames.ProductsView, PermissionNames.ProductsManage],
            SubscriptionModules.Inventory => [PermissionNames.InventoryView, PermissionNames.InventoryManage],
            SubscriptionModules.Sales => [PermissionNames.BillingView, PermissionNames.BillingCreate, PermissionNames.BillingDelete],
            SubscriptionModules.Purchase => [PermissionNames.PurchasesView, PermissionNames.PurchasesManage],
            SubscriptionModules.Payments => [PermissionNames.PaymentsView, PermissionNames.PaymentsManage],
            SubscriptionModules.Reports => [PermissionNames.ReportsView],
            SubscriptionModules.Settings => [PermissionNames.UsersManage, PermissionNames.SettingsManage],
            SubscriptionModules.Expenses => [PermissionNames.ExpensesView, PermissionNames.SettingsManage],
            _ => []
        };
    }
}
