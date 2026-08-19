using AveroNova.Application.Navigation;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Pages.Administration;
using AveroNova.App.UI.Pages.Billing;
using AveroNova.App.UI.Pages.Company;
using AveroNova.App.UI.Pages.Customers;
using AveroNova.App.UI.Pages.Dashboard;
using AveroNova.App.UI.Pages.Inventory;
using AveroNova.App.UI.Pages.Payments;
using AveroNova.App.UI.Pages.Products;
using AveroNova.App.UI.Pages.Profile;
using AveroNova.App.UI.Pages.Purchases;
using AveroNova.App.UI.Pages.Reports;
using AveroNova.App.UI.Pages.Settings;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Layout;

namespace AveroNova.App.UI.Views.Layout;

public partial class MainLayoutView : ContentView
{
    private readonly IConnectivityService _connectivity;
    private readonly IAuthenticationService _auth;
    private readonly ICompanyService _company;
    private readonly ISettingsService _settings;
    private readonly CurrentAccessService _access;
    private readonly TrialReminderPresenter _trialReminder;

    private readonly Func<DashboardPage> _dashboardFactory;
    private readonly Func<CompanyListPage> _companyFactory;
    private readonly Func<CustomersListPage> _customersFactory;
    private readonly Func<ProductsListPage> _productsFactory;
    private readonly Func<InventoryPage> _inventoryFactory;
    private readonly Func<BillingListPage> _billingFactory;
    private readonly Func<PurchasesListPage> _purchasesFactory;
    private readonly Func<PaymentsListPage> _paymentsFactory;
    private readonly Func<ReportsPage> _reportsFactory;
    private readonly Func<UsersRolesPage> _usersRolesFactory;
    private readonly Func<SettingsPage> _settingsFactory;
    private readonly Func<UserProfilePage> _profileFactory;

    private IReadOnlyList<NavigationMenuNode> _menus = [];
    private string? _selectedMenuKey;
    private View? _pageContent;
    private bool _accessStarted;
    private bool _isDesktop = true;

    public MainLayoutView(
        IConnectivityService connectivity,
        IAuthenticationService auth,
        ICompanyService company,
        ISettingsService settings,
        CurrentAccessService access,
        TrialReminderPresenter trialReminder,
        Func<DashboardPage> dashboardFactory,
        Func<CompanyListPage> companyFactory,
        Func<CustomersListPage> customersFactory,
        Func<ProductsListPage> productsFactory,
        Func<InventoryPage> inventoryFactory,
        Func<BillingListPage> billingFactory,
        Func<PurchasesListPage> purchasesFactory,
        Func<PaymentsListPage> paymentsFactory,
        Func<ReportsPage> reportsFactory,
        Func<UsersRolesPage> usersRolesFactory,
        Func<SettingsPage> settingsFactory,
        Func<UserProfilePage> profileFactory)
    {
        AveroNova.App.UI.Helpers.StartupLog.Write("MainLayout ctor start");
        InitializeComponent();
        AveroNova.App.UI.Helpers.StartupLog.Write("MainLayout InitializeComponent done");

        _connectivity = connectivity;
        _auth = auth;
        _company = company;
        _settings = settings;
        _access = access;
        _trialReminder = trialReminder;
        _dashboardFactory = dashboardFactory;
        _companyFactory = companyFactory;
        _customersFactory = customersFactory;
        _productsFactory = productsFactory;
        _inventoryFactory = inventoryFactory;
        _billingFactory = billingFactory;
        _purchasesFactory = purchasesFactory;
        _paymentsFactory = paymentsFactory;
        _reportsFactory = reportsFactory;
        _usersRolesFactory = usersRolesFactory;
        _settingsFactory = settingsFactory;
        _profileFactory = profileFactory;

        _connectivity.StatusChanged += OnConnectivityChanged;
        _company.CurrentCompanyChanged += OnCurrentCompanyChanged;
        SizeChanged += OnLayoutSizeChanged;

        DesktopSidebar.MenuSelected += OnSidebarMenuSelected;
        MobileSidebar.MenuSelected += OnSidebarMenuSelected;
        AccountMenu.ChoiceMade += OnAccountMenuChoice;
        AccountMenu.ThemeChosen += OnAccountThemeChosen;

        AttachTap(HeaderProfile, ToggleAccountMenu);
        AttachTap(MobileProfile, ToggleAccountMenu);
        AttachTap(BtnMenu, OpenDrawer);
        AttachTap(DrawerScrim, CloseDrawer);

        UpdateUserInfo();
        UpdateConnectivityUI(_connectivity.Status);

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        AveroNova.App.UI.Helpers.StartupLog.Write("MainLayout Loaded");
        ApplyResponsiveLayout();
        if (_accessStarted)
            return;

        _accessStarted = true;
        await InitializeAccessAsync();
    }

    private void OnLayoutSizeChanged(object? sender, EventArgs e)
        => ApplyResponsiveLayout();

    private void ApplyResponsiveLayout()
    {
        var width = Width;
        if (width <= 0)
            return;

        var size = ResponsiveBreakpoints.FromWidth(width);
        var compact = size == ScreenSize.Compact;
        var desktop = !compact;
        var dockedWidth = ResponsiveBreakpoints.DockedSidebarWidth(size);
        var compactChanged = desktop != _isDesktop;

        _isDesktop = desktop;

        DesktopLayout.IsVisible = desktop;
        MobileLayout.IsVisible = compact;
        var columnWidth = Math.Max(dockedWidth, 1);
        if (Math.Abs(DesktopSidebarColumnDef.Width.Value - columnWidth) >= 0.5)
            DesktopSidebarColumnDef.Width = new GridLength(columnWidth);
        if (Math.Abs(DrawerPanel.WidthRequest - ResponsiveBreakpoints.SidebarMobileDrawerWidth) >= 0.5)
            DrawerPanel.WidthRequest = ResponsiveBreakpoints.SidebarMobileDrawerWidth;

        var desktopDensity = size == ScreenSize.Medium ? SidebarDensity.Tablet : SidebarDensity.Desktop;
        if (DesktopSidebar.Density != desktopDensity)
            DesktopSidebar.Density = desktopDensity;
        if (MobileSidebar.Density != SidebarDensity.Mobile)
            MobileSidebar.Density = SidebarDensity.Mobile;

        if (desktop)
            CloseDrawer();

        if (compactChanged)
        {
            AccountMenu.Close();
            AttachPageContent();
        }
    }

    private void UpdateUserInfo()
    {
        var user = _auth.CurrentUser;
        if (user == null)
            return;

        var initials = string.IsNullOrWhiteSpace(user.AvatarInitials) ? "AN" : user.AvatarInitials;
        LblHeaderInitials.Text = initials;
        MLblHeaderInitials.Text = initials;
        LblHeaderName.Text = user.Name;
        LblHeaderEmail.Text = user.Email;
    }

    private static void AttachTap(View view, Action handler)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => handler();
        view.GestureRecognizers.Add(tap);
    }

    private void OnConnectivityChanged(object? sender, ConnectivityStatus status)
        => MainThread.BeginInvokeOnMainThread(() => UpdateConnectivityUI(status));

    private void UpdateConnectivityUI(ConnectivityStatus status)
    {
        var (color, label, textColor) = status switch
        {
            ConnectivityStatus.Online => ("#10B981", "Online", "#059669"),
            ConnectivityStatus.Offline => ("#EF4444", "Offline", "#DC2626"),
            ConnectivityStatus.Syncing => ("#3B82F6", "Syncing...", "#2563EB"),
            ConnectivityStatus.Synced => ("#10B981", "Synced", "#059669"),
            ConnectivityStatus.SyncFailed => ("#EF4444", "Sync Failed", "#DC2626"),
            ConnectivityStatus.PendingSync => ("#F59E0B", $"{_connectivity.PendingCount} Pending", "#D97706"),
            _ => ("#9CA3AF", "Unknown", "#6B7280")
        };

        StatusDot.BackgroundColor = Color.FromArgb(color);
        LblConnStatus.Text = label;
        LblConnStatus.TextColor = Color.FromArgb(textColor);
        MStatusDot.BackgroundColor = Color.FromArgb(color);
    }

    private async void OnSidebarMenuSelected(object? sender, string menuKey)
    {
        await NavigateMenuAsync(menuKey);
        var keepOpen = DesktopSidebar.KeepsDrawerOpen(menuKey) || MobileSidebar.KeepsDrawerOpen(menuKey);
        if (!keepOpen)
            CloseDrawer();
    }

    private async Task NavigateMenuAsync(string menuKey)
    {
        if (NavigationMenuCatalog.Find(menuKey) == null)
            return;

        if (!TryResolvePage(menuKey, out var factory, out var title, out var breadcrumb))
        {
            DesktopSidebar.SelectedKey = _selectedMenuKey;
            MobileSidebar.SelectedKey = _selectedMenuKey;
            return;
        }

        var decision = await _access.AuthorizeMenuAsync(menuKey);
        if (!decision.IsAllowed)
        {
            if (decision.IsSubscriptionExpired)
            {
                await SignOutExpiredAsync();
                return;
            }

            ShowRestriction(decision.Reason ?? SubscriptionMessages.PermissionDenied);
            return;
        }

        ShowPage(factory(), title, breadcrumb, menuKey);
    }

    private bool TryResolvePage(
        string menuKey,
        out Func<ContentPage> factory,
        out string title,
        out string breadcrumb)
    {
        factory = null!;
        title = "Dashboard";
        breadcrumb = "Home / Dashboard";

        switch (menuKey)
        {
            case NavigationMenuCatalog.Dashboard:
                factory = () => _dashboardFactory();
                return true;
            case NavigationMenuCatalog.Company:
                factory = () => _companyFactory();
                title = "Company";
                breadcrumb = "Home / Company";
                return true;
            case NavigationMenuCatalog.Customers:
                factory = () => _customersFactory();
                title = "Customers";
                breadcrumb = "Home / Customers";
                return true;
            case NavigationMenuCatalog.Products:
                factory = () => _productsFactory();
                title = "Products";
                breadcrumb = "Home / Products";
                return true;
            case NavigationMenuCatalog.Inventory:
                factory = () => _inventoryFactory();
                title = "Inventory";
                breadcrumb = "Home / Inventory";
                return true;
            case NavigationMenuCatalog.Sales:
                factory = () => _billingFactory();
                title = "Sales";
                breadcrumb = "Home / Sales";
                return true;
            case NavigationMenuCatalog.Purchase:
                factory = () => _purchasesFactory();
                title = "Purchase";
                breadcrumb = "Home / Purchase";
                return true;
            case NavigationMenuCatalog.Payments:
                factory = () => _paymentsFactory();
                title = "Payments";
                breadcrumb = "Home / Payments";
                return true;
            case NavigationMenuCatalog.Reports:
                factory = () => _reportsFactory();
                title = "Reports";
                breadcrumb = "Home / Reports";
                return true;
            case NavigationMenuCatalog.UsersRoles:
                factory = () => _usersRolesFactory();
                title = "Users & Roles";
                breadcrumb = "Home / Administration / Users & Roles";
                return true;
            case NavigationMenuCatalog.Settings:
                factory = () => _settingsFactory();
                title = "Settings";
                breadcrumb = "Home / Settings";
                return true;
            default:
                return false;
        }
    }

    private void ShowPage(ContentPage page, string title, string breadcrumb, string? menuKey)
    {
        AccountMenu.Close();
        CloseDrawer();
        _pageContent = page.Content;
        AttachPageContent();

        LblPageTitle.Text = title;
        LblBreadcrumb.Text = breadcrumb;
        MLblPageTitle.Text = title;
        UpdateUserInfo();
        SetActiveMenu(menuKey);

        if (page is DashboardPage dashboard)
            _ = dashboard.ReloadAsync();
        if (page is UsersRolesPage usersRoles)
            _ = usersRoles.ReloadAsync();
        if (page is UserProfilePage profile)
            profile.Reload();
    }

    private void AttachPageContent()
    {
        ContentArea.Content = null;
        MobileContentArea.Content = null;
        if (_pageContent == null)
            return;

        if (_isDesktop)
            ContentArea.Content = _pageContent;
        else
            MobileContentArea.Content = _pageContent;
    }

    private void SetActiveMenu(string? menuKey)
    {
        _selectedMenuKey = menuKey;
        DesktopSidebar.SelectedKey = menuKey;
        MobileSidebar.SelectedKey = menuKey;
    }

    private async void OpenDrawer()
    {
        AccountMenu.Close();
        DrawerOverlay.IsVisible = true;
        DrawerPanel.TranslationX = -ResponsiveBreakpoints.SidebarMobileDrawerWidth;
        await DrawerPanel.TranslateTo(0, 0, 160, Easing.CubicOut);
    }

    private async void CloseDrawer()
    {
        if (!DrawerOverlay.IsVisible)
            return;

        await DrawerPanel.TranslateTo(-ResponsiveBreakpoints.SidebarMobileDrawerWidth, 0, 140, Easing.CubicIn);
        DrawerOverlay.IsVisible = false;
        DrawerPanel.TranslationX = 0;
    }

    private void ToggleAccountMenu()
    {
        if (AccountMenu.IsOpen)
        {
            AccountMenu.Close();
            return;
        }

        CloseDrawer();
        AccountMenu.Open(
            new HeaderAccountMenuModel
            {
                CanSettings = NavigationMenuCatalog.ContainsKey(_menus, NavigationMenuCatalog.Settings),
                CanAdministrator = NavigationMenuCatalog.ContainsKey(_menus, NavigationMenuCatalog.UsersRoles),
                // Theme replaces the previous ungated header Theme control.
                // Settings.View (settings.manage) still gates the Settings page.
                CanTheme = true,
                CurrentTheme = _settings.Get().Theme
            },
            compact: !_isDesktop);
    }

    private async void OnAccountMenuChoice(object? sender, HeaderAccountMenuChoice choice)
    {
        switch (choice)
        {
            case HeaderAccountMenuChoice.Settings:
                await NavigateMenuAsync(NavigationMenuCatalog.Settings);
                return;
            case HeaderAccountMenuChoice.Administrator:
                await NavigateMenuAsync(NavigationMenuCatalog.UsersRoles);
                return;
            case HeaderAccountMenuChoice.Logout:
                await LogoutAsync();
                return;
        }
    }

    private void OnAccountThemeChosen(object? sender, ThemeMode mode)
        => _settings.SetTheme(mode);

    private async Task LogoutAsync()
    {
        var mainPage = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page;
        if (mainPage == null)
            return;

        var confirm = await mainPage.DisplayAlert(
            "Sign Out",
            "Are you sure you want to sign out?",
            "Sign Out",
            "Cancel");
        if (!confirm)
            return;

        await _auth.LogoutAsync();
        await Shell.Current.GoToAsync(AppRoutes.Login);
    }

    private async Task InitializeAccessAsync()
    {
        try
        {
            var snapshot = await _access.GetSnapshotAsync();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                BindMenus(snapshot.Menus);
                if (snapshot.IsSubscriptionExpired)
                {
                    await SignOutExpiredAsync();
                    return;
                }

                var sidebar = NavigationMenuCatalog.SidebarOnly(snapshot.Menus);
                var target = FindNavigablePage(sidebar, _selectedMenuKey)
                    ?? FindNavigablePage(snapshot.Menus, _selectedMenuKey)
                    ?? FirstNavigablePage(sidebar)
                    ?? FirstNavigablePage(snapshot.Menus);
                if (target == null)
                {
                    ShowRestriction(snapshot.RestrictionReason ?? SubscriptionMessages.PermissionDenied);
                    return;
                }

                await NavigateMenuAsync(target.Key);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Sidebar access failed: {ex}");
        }
    }

    private void BindMenus(IReadOnlyList<NavigationMenuNode> menus)
    {
        _menus = menus ?? [];
        var sidebar = NavigationMenuCatalog.SidebarOnly(_menus);
        AveroNova.App.UI.Helpers.StartupLog.Write(
            $"MainLayout menus={_menus.Count} keys={string.Join(",", _menus.Select(m => m.Children.Count == 0 ? m.Key : $"{m.Key}[{string.Join(",", m.Children.Select(c => c.Key))}]"))} sidebar={string.Join(",", sidebar.Select(m => m.Key))}");
        DesktopSidebar.BindMenus(sidebar, _selectedMenuKey);
        MobileSidebar.BindMenus(sidebar, _selectedMenuKey);
    }

    private NavigationMenuNode? FindNavigablePage(IReadOnlyList<NavigationMenuNode> menus, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        foreach (var item in menus)
        {
            if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
                && !item.IsAccordion
                && HasPage(item.Key))
                return item;
            var child = item.Children.FirstOrDefault(c =>
                c.Key.Equals(key, StringComparison.OrdinalIgnoreCase) && HasPage(c.Key));
            if (child != null)
                return child;
        }

        return null;
    }

    private NavigationMenuNode? FirstNavigablePage(IReadOnlyList<NavigationMenuNode> menus)
    {
        foreach (var item in menus)
        {
            if (!item.IsAccordion && HasPage(item.Key))
                return item;
            var child = item.Children.FirstOrDefault(c => HasPage(c.Key));
            if (child != null)
                return child;
        }

        return null;
    }

    private bool HasPage(string menuKey)
        => TryResolvePage(menuKey, out _, out _, out _);

    private async void OnCurrentCompanyChanged(object? sender, EventArgs e)
    {
        _access.Invalidate();
        await InitializeAccessAsync();
    }

    private async Task SignOutExpiredAsync()
    {
        PendingAuthMessage.Set(SubscriptionMessages.FreeTrialExpiredAccess);
        await _auth.LogoutAsync();
        await Shell.Current.GoToAsync(AppRoutes.Login);
    }

    private void ShowRestriction(string message)
    {
        _pageContent = SubscriptionRestrictionView.Create(message);
        AttachPageContent();
        LblPageTitle.Text = "Subscription";
        LblBreadcrumb.Text = "Home / Subscription";
        MLblPageTitle.Text = "Subscription";
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler != null)
            return;

        SizeChanged -= OnLayoutSizeChanged;
        _connectivity.StatusChanged -= OnConnectivityChanged;
        _company.CurrentCompanyChanged -= OnCurrentCompanyChanged;
        DesktopSidebar.MenuSelected -= OnSidebarMenuSelected;
        MobileSidebar.MenuSelected -= OnSidebarMenuSelected;
        AccountMenu.ChoiceMade -= OnAccountMenuChoice;
        AccountMenu.ThemeChosen -= OnAccountThemeChosen;
        AccountMenu.Close();
    }
}
