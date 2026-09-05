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
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI;

public partial class AppShell : Shell
{
    private readonly IInstallationService _installation;

    public AppShell(IInstallationService installation)
    {
        _installation = installation;
        InitializeComponent();
        RegisterRoutes();
        Navigating += OnNavigating;
    }

    private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        var target = e.Target?.Location?.OriginalString ?? string.Empty;
        if (!target.Contains("Register", StringComparison.OrdinalIgnoreCase))
            return;

        // Status is initialized at app start; block direct Create Account navigation when registered.
        if (_installation.IsRegistered)
        {
            e.Cancel();
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await GoToAsync(AppRoutes.Login);
                }
                catch
                {
                    // ignore navigation races during startup
                }
            });
        }
    }

    private static void RegisterRoutes()
    {
        Routing.RegisterRoute(AppRoutes.Register, typeof(RegisterPage));
        Routing.RegisterRoute(AppRoutes.ForgotPassword, typeof(ForgotPasswordPage));
        Routing.RegisterRoute(AppRoutes.ResetPassword, typeof(ResetPasswordPage));
        Routing.RegisterRoute(AppRoutes.OtpVerify, typeof(OtpVerifyPage));

        Routing.RegisterRoute(AppRoutes.CompanyAdd, typeof(CompanyFormPage));
        Routing.RegisterRoute(AppRoutes.CompanyEdit, typeof(CompanyFormPage));

        Routing.RegisterRoute(AppRoutes.CustomerAdd, typeof(CustomerFormPage));
        Routing.RegisterRoute(AppRoutes.CustomerEdit, typeof(CustomerFormPage));
        Routing.RegisterRoute(AppRoutes.CustomerView, typeof(CustomerViewPage));

        Routing.RegisterRoute(AppRoutes.ProductAdd, typeof(ProductFormPage));
        Routing.RegisterRoute(AppRoutes.ProductEdit, typeof(ProductFormPage));
        Routing.RegisterRoute(AppRoutes.ProductView, typeof(ProductViewPage));

        Routing.RegisterRoute(AppRoutes.StockAdjust, typeof(StockAdjustPage));
        Routing.RegisterRoute(AppRoutes.StockMovement, typeof(StockMovementPage));

        Routing.RegisterRoute(AppRoutes.InvoiceNew, typeof(InvoiceFormPage));
        Routing.RegisterRoute(AppRoutes.InvoiceView, typeof(InvoiceViewPage));
        Routing.RegisterRoute(AppRoutes.InvoiceEdit, typeof(InvoiceFormPage));

        Routing.RegisterRoute(AppRoutes.PurchaseNew, typeof(PurchaseFormPage));
        Routing.RegisterRoute(AppRoutes.PurchaseView, typeof(PurchaseViewPage));

        Routing.RegisterRoute(AppRoutes.PaymentAdd, typeof(PaymentFormPage));
        Routing.RegisterRoute(AppRoutes.PaymentView, typeof(PaymentViewPage));

        Routing.RegisterRoute(AppRoutes.SalesReturnNew, typeof(SalesReturnFormPage));
        Routing.RegisterRoute(AppRoutes.PurchaseReturnNew, typeof(PurchaseReturnFormPage));

        Routing.RegisterRoute(AppRoutes.ExpenseAdd, typeof(ExpenseFormPage));
        Routing.RegisterRoute(AppRoutes.ExpenseView, typeof(ExpenseViewPage));

        Routing.RegisterRoute(AppRoutes.UserAdd, typeof(UserFormPage));
        Routing.RegisterRoute(AppRoutes.UserView, typeof(UserViewPage));
        Routing.RegisterRoute(AppRoutes.UserEdit, typeof(UserFormPage));
        Routing.RegisterRoute(AppRoutes.RoleAdd, typeof(RoleFormPage));
        Routing.RegisterRoute(AppRoutes.RoleEdit, typeof(RoleFormPage));

        Routing.RegisterRoute(AppRoutes.Notifications, typeof(NotificationsPage));
        Routing.RegisterRoute(AppRoutes.Help, typeof(HelpAboutPage));
    }
}
