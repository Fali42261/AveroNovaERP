using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Pages.Administration;
using AveroNova.App.UI.Pages.Billing;
using AveroNova.App.UI.Pages.Company;
using AveroNova.App.UI.Pages.Customers;
using AveroNova.App.UI.Pages.Dashboard;
using AveroNova.App.UI.Pages.Expenses;
using AveroNova.App.UI.Pages.Help;
using AveroNova.App.UI.Pages.Inventory;
using AveroNova.App.UI.Pages.Payments;
using AveroNova.App.UI.Pages.Products;
using AveroNova.App.UI.Pages.Purchases;
using AveroNova.App.UI.Pages.Reports;
using AveroNova.App.UI.Pages.Returns;
using AveroNova.App.UI.Pages.Settings;
using AveroNova.App.UI.Pages.Subscription;
using AveroNova.App.UI.Pages.SyncCenter;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls;
using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Views.Layout;

public partial class MainLayoutView : ContentView
{
    private readonly IConnectivityService _connectivity;
    private readonly IAuthenticationService _auth;
    private readonly ICompanyService _company;

    private Button? _activeNavButton;

    private readonly Func<DashboardPage> _dashboardFactory;
    private readonly Func<CompanyListPage> _companyFactory;
    private readonly Func<CustomersListPage> _customersFactory;
    private readonly Func<ProductsListPage> _productsFactory;
    private readonly Func<InventoryPage> _inventoryFactory;
    private readonly Func<BillingListPage> _billingFactory;
    private readonly Func<PurchasesListPage> _purchasesFactory;
    private readonly Func<PaymentsListPage> _paymentsFactory;
    private readonly Func<SalesReturnsListPage> _salesReturnsFactory;
    private readonly Func<PurchaseReturnsListPage> _purchaseReturnsFactory;
    private readonly Func<ExpensesListPage> _expensesFactory;
    private readonly Func<ReportsPage> _reportsFactory;
    private readonly Func<UsersListPage> _usersFactory;
    private readonly Func<RolesListPage> _rolesFactory;
    private readonly Func<PermissionsPage> _permissionsFactory;
    private readonly Func<SubscriptionPage> _subscriptionFactory;
    private readonly Func<SyncCenterPage> _syncCenterFactory;
    private readonly Func<SettingsPage> _settingsFactory;
    private readonly Func<HelpAboutPage> _helpFactory;

    public MainLayoutView(
        IConnectivityService connectivity,
        IAuthenticationService auth,
        ICompanyService company,
        Func<DashboardPage> dashboardFactory,
        Func<CompanyListPage> companyFactory,
        Func<CustomersListPage> customersFactory,
        Func<ProductsListPage> productsFactory,
        Func<InventoryPage> inventoryFactory,
        Func<BillingListPage> billingFactory,
        Func<PurchasesListPage> purchasesFactory,
        Func<PaymentsListPage> paymentsFactory,
        Func<SalesReturnsListPage> salesReturnsFactory,
        Func<PurchaseReturnsListPage> purchaseReturnsFactory,
        Func<ExpensesListPage> expensesFactory,
        Func<ReportsPage> reportsFactory,
        Func<UsersListPage> usersFactory,
        Func<RolesListPage> rolesFactory,
        Func<PermissionsPage> permissionsFactory,
        Func<SubscriptionPage> subscriptionFactory,
        Func<SyncCenterPage> syncCenterFactory,
        Func<SettingsPage> settingsFactory,
        Func<HelpAboutPage> helpFactory)
    {
        InitializeComponent();

        _connectivity = connectivity;
        _auth = auth;
        _company = company;

        _dashboardFactory = dashboardFactory;
        _companyFactory = companyFactory;
        _customersFactory = customersFactory;
        _productsFactory = productsFactory;
        _inventoryFactory = inventoryFactory;
        _billingFactory = billingFactory;
        _purchasesFactory = purchasesFactory;
        _paymentsFactory = paymentsFactory;
        _salesReturnsFactory = salesReturnsFactory;
        _purchaseReturnsFactory = purchaseReturnsFactory;
        _expensesFactory = expensesFactory;
        _reportsFactory = reportsFactory;
        _usersFactory = usersFactory;
        _rolesFactory = rolesFactory;
        _permissionsFactory = permissionsFactory;
        _subscriptionFactory = subscriptionFactory;
        _syncCenterFactory = syncCenterFactory;
        _settingsFactory = settingsFactory;
        _helpFactory = helpFactory;

        _connectivity.StatusChanged += OnConnectivityChanged;

        UpdateUserInfo();
        UpdateCompanyInfo();
        UpdateConnectivityUI(_connectivity.Status);

        ShowDesktopPage(
            _dashboardFactory(),
            "Dashboard",
            "Home / Dashboard",
            BtnDashboard);

        ShowMobilePage(
            _dashboardFactory(),
            "Dashboard",
            "Home / Dashboard");
    }

    // ============================================================
    // USER
    // ============================================================

    private void UpdateUserInfo()
    {
        var user = _auth.CurrentUser;

        if (user == null)
            return;

        LblAvatarInitials.Text = user.AvatarInitials;
        LblHeaderInitials.Text = user.AvatarInitials;
        LblUserName.Text = user.Name;
        LblUserRole.Text = user.Role;
    }

    // ============================================================
    // COMPANY
    // ============================================================

    private void UpdateCompanyInfo()
    {
        var company = _company.CurrentCompany;

        if (company == null)
            return;

        var name = company.Name ?? string.Empty;

        LblCompanyName.Text =
            name.Length > 18
                ? name[..18] + "…"
                : name;
    }

    // ============================================================
    // CONNECTIVITY
    // ============================================================

    private void OnConnectivityChanged(
        object? sender,
        ConnectivityStatus status)
    {
        MainThread.BeginInvokeOnMainThread(
            () => UpdateConnectivityUI(status));
    }

    private void UpdateConnectivityUI(
        ConnectivityStatus status)
    {
        var (color, label, bgColor, borderColor, textColor) =
            status switch
            {
                ConnectivityStatus.Online =>
                    ("#10B981", "Online", "#ECFDF5", "#A7F3D0", "#059669"),

                ConnectivityStatus.Offline =>
                    ("#EF4444", "Offline", "#FEF2F2", "#FECACA", "#DC2626"),

                ConnectivityStatus.Syncing =>
                    ("#3B82F6", "Syncing...", "#EFF6FF", "#BFDBFE", "#2563EB"),

                ConnectivityStatus.Synced =>
                    ("#10B981", "Synced", "#ECFDF5", "#A7F3D0", "#059669"),

                ConnectivityStatus.SyncFailed =>
                    ("#EF4444", "Sync Failed", "#FEF2F2", "#FECACA", "#DC2626"),

                ConnectivityStatus.PendingSync =>
                    ("#F59E0B",
                     $"{_connectivity.PendingCount} Pending",
                     "#FFFBEB",
                     "#FDE68A",
                     "#D97706"),

                _ =>
                    ("#9CA3AF",
                     "Unknown",
                     "#F3F4F6",
                     "#E5E7EB",
                     "#6B7280")
            };

        StatusDot.BackgroundColor = Color.FromArgb(color);
        MStatusDot.BackgroundColor = Color.FromArgb(color);

        LblConnStatus.Text = label;
        LblConnStatus.TextColor = Color.FromArgb(textColor);
    }

    // ============================================================
    // NAVIGATION
    // ============================================================

    private void OnNavClicked(
        object? sender,
        EventArgs e)
    {
        if (sender is not Button button)
            return;

        var isMobile = IsMobileButton(button);

        if (TryResolvePage(
                button,
                out var pageFactory,
                out var title,
                out var breadcrumb))
        {
            if (isMobile)
            {
                ShowMobilePage(
                    pageFactory(),
                    title,
                    breadcrumb);
            }
            else
            {
                ShowDesktopPage(
                    pageFactory(),
                    title,
                    breadcrumb,
                    button);
            }
        }
    }

    private bool IsMobileButton(Button button)
    {
        return ReferenceEquals(button, MBtnDashboard)
            || ReferenceEquals(button, MBtnBilling)
            || ReferenceEquals(button, MBtnCustomers)
            || ReferenceEquals(button, MBtnReports)
            || ReferenceEquals(button, MBtnSettings);
    }

    private bool TryResolvePage(
        Button button,
        out Func<ContentPage> factory,
        out string title,
        out string breadcrumb)
    {
        factory = null!;
        title = "Dashboard";
        breadcrumb = "Home / Dashboard";

        if (ReferenceEquals(button, BtnDashboard) ||
            ReferenceEquals(button, MBtnDashboard))
        {
            factory = () => _dashboardFactory();
            title = "Dashboard";
            breadcrumb = "Home / Dashboard";
            return true;
        }

        if (ReferenceEquals(button, BtnCompany))
        {
            factory = () => _companyFactory();
            title = "Company";
            breadcrumb = "Home / Company";
            return true;
        }

        if (ReferenceEquals(button, BtnCustomers) ||
            ReferenceEquals(button, MBtnCustomers))
        {
            factory = () => _customersFactory();
            title = "Customers";
            breadcrumb = "Home / Customers";
            return true;
        }

        if (ReferenceEquals(button, BtnProducts))
        {
            factory = () => _productsFactory();
            title = "Products";
            breadcrumb = "Home / Products";
            return true;
        }

        if (ReferenceEquals(button, BtnInventory))
        {
            factory = () => _inventoryFactory();
            title = "Inventory";
            breadcrumb = "Home / Inventory";
            return true;
        }

        if (ReferenceEquals(button, BtnBilling) ||
            ReferenceEquals(button, MBtnBilling))
        {
            factory = () => _billingFactory();
            title = "Billing";
            breadcrumb = "Home / Billing";
            return true;
        }

        if (ReferenceEquals(button, BtnPurchases))
        {
            factory = () => _purchasesFactory();
            title = "Purchases";
            breadcrumb = "Home / Purchases";
            return true;
        }

        if (ReferenceEquals(button, BtnPayments))
        {
            factory = () => _paymentsFactory();
            title = "Payments";
            breadcrumb = "Home / Payments";
            return true;
        }

        if (ReferenceEquals(button, BtnSalesReturns))
        {
            factory = () => _salesReturnsFactory();
            title = "Sales Returns";
            breadcrumb = "Home / Sales Returns";
            return true;
        }

        if (ReferenceEquals(button, BtnPurchaseReturns))
        {
            factory = () => _purchaseReturnsFactory();
            title = "Purchase Returns";
            breadcrumb = "Home / Purchase Returns";
            return true;
        }

        if (ReferenceEquals(button, BtnExpenses))
        {
            factory = () => _expensesFactory();
            title = "Expenses";
            breadcrumb = "Home / Expenses";
            return true;
        }

        if (ReferenceEquals(button, BtnReports) ||
            ReferenceEquals(button, MBtnReports))
        {
            factory = () => _reportsFactory();
            title = "Reports";
            breadcrumb = "Home / Reports";
            return true;
        }

        if (ReferenceEquals(button, BtnUsers))
        {
            factory = () => _usersFactory();
            title = "Users";
            breadcrumb = "Home / Administration / Users";
            return true;
        }

        if (ReferenceEquals(button, BtnRoles))
        {
            factory = () => _rolesFactory();
            title = "Roles";
            breadcrumb = "Home / Administration / Roles";
            return true;
        }

        if (ReferenceEquals(button, BtnPermissions))
        {
            factory = () => _permissionsFactory();
            title = "Permissions";
            breadcrumb = "Home / Administration / Permissions";
            return true;
        }

        if (ReferenceEquals(button, BtnSubscription))
        {
            factory = () => _subscriptionFactory();
            title = "Subscription";
            breadcrumb = "Home / Subscription";
            return true;
        }

        if (ReferenceEquals(button, BtnSyncCenter))
        {
            factory = () => _syncCenterFactory();
            title = "Sync Center";
            breadcrumb = "Home / Sync Center";
            return true;
        }

        if (ReferenceEquals(button, BtnSettings) ||
            ReferenceEquals(button, MBtnSettings))
        {
            factory = () => _settingsFactory();
            title = "Settings";
            breadcrumb = "Home / Settings";
            return true;
        }

        if (ReferenceEquals(button, BtnHelp) ||
            ReferenceEquals(button, BtnAbout))
        {
            factory = () => _helpFactory();
            title = "Help & Support";
            breadcrumb = "Home / Help";
            return true;
        }

        if (ReferenceEquals(button, BtnNotifications))
        {
            factory = () => _dashboardFactory();
            title = "Notifications";
            breadcrumb = "Home / Notifications";
            return true;
        }

        return false;
    }

    // ============================================================
    // PAGE HOSTING
    // ============================================================

    private static View? ExtractPageContent(ContentPage page)
    {
        return page.Content;
    }

    private void ShowDesktopPage(
        ContentPage page,
        string title,
        string breadcrumb,
        Button? navButton = null)
    {
        var content = ExtractPageContent(page);

        if (content != null)
        {
            ContentArea.Content = content;
        }

        LblPageTitle.Text = title;
        LblBreadcrumb.Text = breadcrumb;

        if (_activeNavButton != null)
        {
            _activeNavButton.Style =
                (Style)Resources["SidebarNavItem"];
        }

        if (navButton != null)
        {
            navButton.Style =
                (Style)Resources["SidebarNavItemActive"];

            _activeNavButton = navButton;
        }
    }

    private void ShowMobilePage(
        ContentPage page,
        string title,
        string breadcrumb)
    {
        var content = ExtractPageContent(page);

        if (content != null)
        {
            MobileContentArea.Content = content;
        }

        MLblPageTitle.Text = title;
    }

    // ============================================================
    // LOGOUT
    // ============================================================

    private async void OnLogoutClicked(
        object? sender,
        EventArgs e)
    {
        var mainPage = Microsoft.Maui.Controls.Application.Current?.Windows
            .FirstOrDefault()?
            .Page;

        if (mainPage == null)
            return;

        bool confirm = await mainPage.DisplayAlert(
            "Sign Out",
            "Are you sure you want to sign out?",
            "Sign Out",
            "Cancel");

        if (!confirm)
            return;

        await _auth.LogoutAsync();
        await Shell.Current.GoToAsync(AppRoutes.Login);
    }

    // ============================================================
    // THEME
    // ============================================================

    private void OnThemeClicked(
        object? sender,
        EventArgs e)
    {
        var current =
            Microsoft.Maui.Controls.Application.Current?.UserAppTheme
            ?? AppTheme.Unspecified;

        Microsoft.Maui.Controls.Application.Current!.UserAppTheme =
            current == AppTheme.Dark
                ? AppTheme.Light
                : AppTheme.Dark;
    }

    // ============================================================
    // RESPONSIVE
    // ============================================================

    protected override void OnSizeAllocated(
        double width,
        double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0)
            return;

        bool desktop = width >= AveroNova.App.UI.Layout.ResponsiveBreakpoints.ShellDesktopMinWidth;

        DesktopLayout.IsVisible = desktop;
        MobileLayout.IsVisible = !desktop;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler == null)
        {
            _connectivity.StatusChanged -=
                OnConnectivityChanged;
        }
    }
}