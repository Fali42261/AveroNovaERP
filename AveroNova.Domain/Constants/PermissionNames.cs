namespace AveroNova.Domain.Constants;

/// <summary>
/// Existing permission keys stored in <c>Permissions.PermissionName</c>.
/// Do not invent parallel names for the same capability.
/// </summary>
public static class PermissionNames
{
    public const string DashboardView = "dashboard.view";
    public const string BillingView = "billing.view";
    public const string BillingCreate = "billing.create";
    public const string BillingDelete = "billing.delete";
    public const string CustomersView = "customers.view";
    public const string CustomersManage = "customers.manage";
    public const string ProductsView = "products.view";
    public const string ProductsManage = "products.manage";
    public const string InventoryView = "inventory.view";
    public const string InventoryManage = "inventory.manage";
    public const string PaymentsView = "payments.view";
    public const string PaymentsManage = "payments.manage";
    public const string ReportsView = "reports.view";
    public const string UsersManage = "users.manage";
    public const string SettingsManage = "settings.manage";
    public const string SubscriptionView = "subscription.view";
    public const string CompanyView = "company.view";
    public const string PurchasesView = "purchases.view";
    public const string PurchasesManage = "purchases.manage";
    public const string ExpensesView = "expenses.view";
}
