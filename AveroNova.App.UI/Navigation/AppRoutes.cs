namespace AveroNova.App.UI.Navigation;

/// <summary>
/// Centralized route constants for Shell navigation.
/// Use these instead of magic strings throughout the application.
/// </summary>
public static class AppRoutes
{
    // Auth
    public const string Splash         = "//Splash";
    public const string Welcome        = "//Welcome";
    public const string Login          = "//Login";
    public const string Register       = "Register";
    public const string ForgotPassword = "ForgotPassword";
    public const string ResetPassword  = "ResetPassword";
    public const string OtpVerify      = "OtpVerify";

    // Main shell
    public const string Main           = "//Main";

    // Business
    public const string Dashboard      = "Dashboard";
    public const string Company        = "Company";
    public const string CompanyAdd     = "CompanyAdd";
    public const string CompanyEdit    = "CompanyEdit";
    public const string Customers      = "Customers";
    public const string CustomerAdd    = "CustomerAdd";
    public const string CustomerEdit   = "CustomerEdit";
    public const string CustomerView   = "CustomerView";
    public const string Products       = "Products";
    public const string ProductAdd     = "ProductAdd";
    public const string ProductEdit    = "ProductEdit";
    public const string ProductView    = "ProductView";
    public const string Inventory      = "Inventory";
    public const string StockAdjust    = "StockAdjust";
    public const string StockMovement  = "StockMovement";
    public const string Billing        = "Billing";
    public const string InvoiceNew     = "InvoiceNew";
    public const string InvoiceView    = "InvoiceView";
    public const string InvoiceEdit    = "InvoiceEdit";
    public const string Purchases      = "Purchases";
    public const string PurchaseNew    = "PurchaseNew";
    public const string PurchaseView   = "PurchaseView";
    public const string Payments       = "Payments";
    public const string PaymentAdd     = "PaymentAdd";
    public const string PaymentView    = "PaymentView";
    public const string SalesReturns   = "SalesReturns";
    public const string SalesReturnNew = "SalesReturnNew";
    public const string PurchaseReturns   = "PurchaseReturns";
    public const string PurchaseReturnNew = "PurchaseReturnNew";
    public const string Expenses       = "Expenses";
    public const string ExpenseAdd     = "ExpenseAdd";
    public const string ExpenseView    = "ExpenseView";
    public const string ExpenseEdit    = "ExpenseEdit";

    // Reports
    public const string Reports        = "Reports";

    // Administration
    public const string Users          = "Users";
    public const string UserAdd        = "UserAdd";
    public const string UserView       = "UserView";
    public const string UserEdit       = "UserEdit";
    public const string Roles          = "Roles";
    public const string RoleAdd        = "RoleAdd";
    public const string RoleEdit       = "RoleEdit";
    public const string Permissions    = "Permissions";
    public const string UsersRoles     = "UsersRoles";
    public const string Profile        = "Profile";

    // Subscription
    public const string Subscription   = "Subscription";
    public const string Plans          = "Plans";

    // System
    public const string Notifications  = "Notifications";
    public const string SyncCenter     = "SyncCenter";
    public const string Settings       = "Settings";
    public const string Help           = "Help";
    public const string About          = "About";
}
