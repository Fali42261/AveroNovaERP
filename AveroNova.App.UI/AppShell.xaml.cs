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
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;

namespace AveroNova.App.UI;

public partial class AppShell : Shell
{
    private readonly CurrentAccessService _access;
    private readonly IAuthenticationService _auth;

    public AppShell(CurrentAccessService access, IAuthenticationService auth)
    {
        AveroNova.App.UI.Helpers.StartupLog.Write("AppShell ctor start");
        InitializeComponent();
        AveroNova.App.UI.Helpers.StartupLog.Write("AppShell InitializeComponent done");
        _access = access;
        _auth = auth;
        RegisterRoutes();
        Navigating += OnNavigating;
    }

    private async void OnNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        try
        {
            await AuthorizeNavigationAsync(e);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Navigation guard failed: {ex}");
        }
    }

    private async Task AuthorizeNavigationAsync(ShellNavigatingEventArgs e)
    {
        if (e.Source is ShellNavigationSource.Pop or ShellNavigationSource.PopToRoot)
            return;

        var target = e.Target?.Location?.OriginalString;
        if (IsPublicRoute(target))
            return;

        var isMain = IsMainRoute(target);
        var hasFeature = FeatureRouteMap.TryResolve(target, out var module, out var permission);
        if (!isMain && !hasFeature)
            return;

        // Main/Dashboard requires an authenticated session from Login (or splash restore).
        // A leftover Preferences user id after registration must not open Dashboard.
        if (!_auth.IsAuthenticated)
        {
            e.Cancel();
            await GoToLoginAsync();
            return;
        }

        if (isMain && !hasFeature)
            return;

        var decision = string.IsNullOrWhiteSpace(permission)
            ? await _access.AuthorizeAsync(module)
            : await _access.AuthorizeFeatureAsync(module, permission);
        if (decision.IsAllowed)
            return;

        e.Cancel();
        if (decision.IsSubscriptionExpired)
        {
            await SignOutExpiredAsync();
            return;
        }

        var message = decision.Reason ?? SubscriptionMessages.FreeTrialExpiredAccess;
        var page = Current?.CurrentPage;
        if (page != null)
            await page.DisplayAlertAsync("Subscription", message, "OK");
    }

    private async Task SignOutExpiredAsync()
    {
        PendingAuthMessage.Set(SubscriptionMessages.FreeTrialExpiredAccess);
        await _auth.LogoutAsync();
        await GoToLoginAsync();
    }

    private Task GoToLoginAsync()
        => GoToAsync(AppRoutes.Login);

    private static bool IsMainRoute(string? location)
        => !string.IsNullOrWhiteSpace(location) && ContainsRoute(location, "Main");

    private static bool IsPublicRoute(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return true;

        return ContainsRoute(location, "Splash")
               || ContainsRoute(location, "Welcome")
               || ContainsRoute(location, "Login")
               || ContainsRoute(location, "Register")
               || ContainsRoute(location, "ForgotPassword")
               || ContainsRoute(location, "ResetPassword")
               || ContainsRoute(location, "OtpVerify");
    }

    private static bool ContainsRoute(string location, string route)
        => location.Equals(route, StringComparison.OrdinalIgnoreCase)
           || location.Contains("//" + route, StringComparison.OrdinalIgnoreCase)
           || location.Contains("/" + route, StringComparison.OrdinalIgnoreCase);

    private static void RegisterRoutes()
    {
        Routing.RegisterRoute(AppRoutes.Register,       typeof(RegisterPage));
        Routing.RegisterRoute(AppRoutes.ForgotPassword, typeof(ForgotPasswordPage));
        Routing.RegisterRoute(AppRoutes.ResetPassword,  typeof(ResetPasswordPage));
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
