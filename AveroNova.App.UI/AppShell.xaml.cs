using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Pages.Administration;
using AveroNova.App.UI.Pages.Authentication;
using AveroNova.App.UI.Pages.Billing;
using AveroNova.App.UI.Pages.Company;
using AveroNova.App.UI.Pages.Customers;
using AveroNova.App.UI.Pages.Expenses;
using AveroNova.App.UI.Pages.Help;
using AveroNova.App.UI.Pages.Inventory;
using AveroNova.App.UI.Pages.Notifications;
using AveroNova.App.UI.Pages.Payments;
using AveroNova.App.UI.Pages.Products;
using AveroNova.App.UI.Pages.Purchases;
using AveroNova.App.UI.Pages.Returns;
using AveroNova.App.UI.Pages.Settings;
using AveroNova.App.UI.Pages.Subscription;
using AveroNova.App.UI.Pages.SyncCenter;

namespace AveroNova.App.UI;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        RegisterRoutes();
    }

    private static void RegisterRoutes()
    {
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        // Auth push routes
        Routing.RegisterRoute(AppRoutes.Register,       typeof(RegisterPage));
        Routing.RegisterRoute(AppRoutes.ForgotPassword, typeof(ForgotPasswordPage));
        Routing.RegisterRoute(AppRoutes.OtpVerify,      typeof(OtpVerifyPage));

        // Company
        Routing.RegisterRoute(AppRoutes.CompanyAdd,  typeof(CompanyFormPage));
        Routing.RegisterRoute(AppRoutes.CompanyEdit, typeof(CompanyFormPage));

        // Customers
        Routing.RegisterRoute(AppRoutes.CustomerAdd,  typeof(CustomerFormPage));
        Routing.RegisterRoute(AppRoutes.CustomerEdit, typeof(CustomerFormPage));
        Routing.RegisterRoute(AppRoutes.CustomerView, typeof(CustomerViewPage));

        // Products
        Routing.RegisterRoute(AppRoutes.ProductAdd,  typeof(ProductFormPage));
        Routing.RegisterRoute(AppRoutes.ProductEdit, typeof(ProductFormPage));
        Routing.RegisterRoute(AppRoutes.ProductView, typeof(ProductViewPage));

        // Inventory
        Routing.RegisterRoute(AppRoutes.StockAdjust,   typeof(StockAdjustPage));
        Routing.RegisterRoute(AppRoutes.StockMovement,  typeof(StockMovementPage));

        // Billing
        Routing.RegisterRoute(AppRoutes.InvoiceNew,  typeof(InvoiceFormPage));
        Routing.RegisterRoute(AppRoutes.InvoiceView, typeof(InvoiceViewPage));
        Routing.RegisterRoute(AppRoutes.InvoiceEdit, typeof(InvoiceFormPage));

        // Purchases
        Routing.RegisterRoute(AppRoutes.PurchaseNew,  typeof(PurchaseFormPage));
        Routing.RegisterRoute(AppRoutes.PurchaseView, typeof(PurchaseViewPage));

        // Payments
        Routing.RegisterRoute(AppRoutes.PaymentAdd,  typeof(PaymentFormPage));
        Routing.RegisterRoute(AppRoutes.PaymentView, typeof(PaymentViewPage));

        // Returns
        Routing.RegisterRoute(AppRoutes.SalesReturnNew,   typeof(SalesReturnFormPage));
        Routing.RegisterRoute(AppRoutes.PurchaseReturnNew, typeof(PurchaseReturnFormPage));

        // Expenses
        Routing.RegisterRoute(AppRoutes.ExpenseAdd,  typeof(ExpenseFormPage));
        Routing.RegisterRoute(AppRoutes.ExpenseView, typeof(ExpenseViewPage));

        // Administration
        Routing.RegisterRoute(AppRoutes.UserAdd,  typeof(UserFormPage));
        Routing.RegisterRoute(AppRoutes.UserView, typeof(UserViewPage));
        Routing.RegisterRoute(AppRoutes.UserEdit, typeof(UserFormPage));
        Routing.RegisterRoute(AppRoutes.RoleAdd,  typeof(RoleFormPage));
        Routing.RegisterRoute(AppRoutes.RoleEdit, typeof(RoleFormPage));

        // System
        Routing.RegisterRoute(AppRoutes.Notifications, typeof(NotificationsPage));
        //Routing.RegisterRoute(AppRoutes.Help,           typeof(HelpPage));
        Routing.RegisterRoute(AppRoutes.Help, typeof(HelpAboutPage));
    }
}
