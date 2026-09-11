using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;

namespace AveroNova.App.UI.Views.Layout;

public partial class MainLayoutView
{
    private bool _productUxConnectivityHooked;
    private bool _productUxNavigationHooked;
    private bool _productUxLoadedHooked;
    private bool _initialDashboardLoaded;

    protected override void OnParentSet()
    {
        base.OnParentSet();

        if (Parent is not null)
        {
            HideOfflineBanners();
            ConfigureMobileNavigation();
            ApplySwipeDigitBranding(this);

            if (!_productUxConnectivityHooked)
            {
                _connectivity.StatusChanged += OnProductUxConnectivityChanged;
                _productUxConnectivityHooked = true;
            }

            if (!_productUxNavigationHooked)
            {
                _contentNavigator.PageChanged += OnProductUxPageChanged;
                _productUxNavigationHooked = true;
            }

            if (!_productUxLoadedHooked)
            {
                Loaded += OnProductUxLoaded;
                _productUxLoadedHooked = true;
            }
        }
        else
        {
            if (_productUxConnectivityHooked)
            {
                _connectivity.StatusChanged -= OnProductUxConnectivityChanged;
                _productUxConnectivityHooked = false;
            }

            if (_productUxNavigationHooked)
            {
                _contentNavigator.PageChanged -= OnProductUxPageChanged;
                _productUxNavigationHooked = false;
            }

            if (_productUxLoadedHooked)
            {
                Loaded -= OnProductUxLoaded;
                _productUxLoadedHooked = false;
            }

            MBtnSettings.Clicked -= OnMoreClicked;
        }
    }

    private async void OnProductUxLoaded(object? sender, EventArgs e)
    {
        ApplySwipeDigitBranding(this);
        BtnSyncCenter.IsVisible = false;

        if (_initialDashboardLoaded) return;
        _initialDashboardLoaded = true;

        try
        {
            var page = _dashboardFactory();
            ShowMobilePage(page, "Dashboard", "Home / Dashboard");
            _contentNavigator.SetRoot(page, "Dashboard", "Home / Dashboard");
            UpdateMobileNavSelection("Dashboard");

            if (page is IHostedPage hosted)
                await hosted.LoadForHostAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Initial load failed: {ex}");
        }
    }

    private void ConfigureMobileNavigation()
    {
        MBtnSettings.Text = "More";
        MBtnSettings.Clicked -= OnNavClicked;
        MBtnSettings.Clicked -= OnMoreClicked;
        MBtnSettings.Clicked += OnMoreClicked;
        BtnSyncCenter.IsVisible = false;
        UpdateMobileNavSelection("Dashboard");
    }

    private void OnProductUxConnectivityChanged(object? sender, ConnectivityStatus status)
        => MainThread.BeginInvokeOnMainThread(HideOfflineBanners);

    private void OnProductUxPageChanged(object? sender, HostedPage entry)
        => MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateMobileNavSelection(entry.Title);
            ApplySwipeDigitBranding(this);
            BtnSyncCenter.IsVisible = false;
        });

    private void HideOfflineBanners()
    {
        OfflineBanner.IsVisible = false;
        MOfflineBanner.IsVisible = false;
    }

    private void UpdateMobileNavSelection(string? title)
    {
        var secondary = ResolveColor("TextSecondary", "#64748B");
        var primary = ResolveColor("PrimaryColor", "#2563EB");
        var buttons = new[] { MBtnDashboard, MBtnBilling, MBtnCustomers, MBtnReports, MBtnSettings };

        foreach (var button in buttons)
        {
            button.TextColor = secondary;
            button.FontAttributes = FontAttributes.None;
            button.BackgroundColor = Colors.Transparent;
        }

        Button active = title switch
        {
            "Dashboard" => MBtnDashboard,
            "Billing" or "New Invoice" or "Edit Invoice" => MBtnBilling,
            "Customers" or "Add Customer" or "Edit Customer" or "Customer Details" => MBtnCustomers,
            "Reports" => MBtnReports,
            _ => MBtnSettings
        };

        active.TextColor = primary;
        active.FontAttributes = FontAttributes.Bold;
    }

    private async void OnMoreClicked(object? sender, EventArgs e)
    {
        try
        {
            var permissions = _session.Permissions;
            var entries = BuildMobileMoreEntries()
                .Where(x => MenuCatalog.IsAllowed(x.PermissionKey, permissions))
                .ToList();

            if (entries.Count == 0)
            {
                await ShowMessageAsync("Menu", "No additional menu items are available for this account.");
                return;
            }

            var choice = await GetHostPage().DisplayActionSheet(
                "SwipeDigit Menu",
                "Cancel",
                null,
                entries.Select(x => x.Label).ToArray());

            if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel") return;

            var selected = entries.FirstOrDefault(x => x.Label == choice);
            if (selected is null) return;

            var exemptFromLicense = selected.PermissionKey is "License" or "Settings" or "Help";
            if (!exemptFromLicense)
            {
                var access = await _licenses.GetAccessStateAsync();
                if (!access.AllowsAccess)
                {
                    await NavigateMobileAsync(() => _licenseFactory(), "Plan", "Home / Plan");
                    return;
                }
            }

            await NavigateMobileAsync(selected.Factory, selected.Title, selected.Breadcrumb);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainMenu] More menu failed: {ex}");
            await ShowMessageAsync("Menu", "Menu could not be opened. Please try again.");
        }
    }

    private List<MobileMenuEntry> BuildMobileMoreEntries() =>
    [
        new("Company", "Company", "Company", "Home / Company", () => _companyFactory()),
        new("Products", "Products", "Products", "Home / Products", () => _productsFactory()),
        new("Inventory", "Inventory", "Inventory", "Home / Inventory", () => _inventoryFactory()),
        new("Purchases", "Purchases", "Purchases", "Home / Purchases", () => _purchasesFactory()),
        new("Payments", "Payments", "Payments", "Home / Payments", () => _paymentsFactory()),
        new("SalesReturns", "Sales Returns", "Sales Returns", "Home / Sales Returns", () => _salesReturnsFactory()),
        new("PurchaseReturns", "Purchase Returns", "Purchase Returns", "Home / Purchase Returns", () => _purchaseReturnsFactory()),
        new("Expenses", "Expenses", "Expenses", "Home / Expenses", () => _expensesFactory()),
        new("Users", "Users", "Users", "Home / Administration / Users", () => _usersFactory()),
        new("Roles", "Roles", "Roles", "Home / Administration / Roles", () => _rolesFactory()),
        new("Permissions", "Permissions", "Permissions", "Home / Administration / Permissions", () => _permissionsFactory()),
        new("Notifications", "Notifications", "Notifications", "Home / Notifications", () => _notificationsFactory()),
        new("Settings", "Settings", "Settings", "Home / Settings", () => _settingsFactory()),
        new("License", "Plan", "Plan", "Home / Plan", () => _licenseFactory()),
        new("Help", "Help & Support", "Help & Support", "Home / Help", () => _helpFactory())
    ];

    private async Task NavigateMobileAsync(Func<ContentPage> factory, string title, string breadcrumb)
    {
        ContentPage page;
        try
        {
            page = factory();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainMenu] Factory failed for {title}: {ex}");
            await ShowMessageAsync(title, $"{title} could not be opened.");
            return;
        }

        ShowMobilePage(page, title, breadcrumb);
        _contentNavigator.SetRoot(page, title, breadcrumb);
        UpdateMobileNavSelection(title);

        if (page is IHostedPage hosted)
        {
            try
            {
                await hosted.LoadForHostAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainMenu] Hosted load failed for {title}: {ex}");
                await ShowMessageAsync(title, $"{title} opened, but its data could not be loaded. Pull down to retry.");
            }
        }
    }

    private Page GetHostPage()
    {
        var page = Microsoft.Maui.Controls.Application.Current?.Windows
            .FirstOrDefault(w => w.Page is not null)?.Page;
        if (page is not null) return page;
        if (Window?.Page is Page windowPage) return windowPage;
        return Shell.Current ?? throw new InvalidOperationException("No active page is available.");
    }

    private async Task ShowMessageAsync(string title, string message)
        => await GetHostPage().DisplayAlert(title, message, "OK");

    private static Color ResolveColor(string resourceKey, string fallback)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(resourceKey, out var value) == true
            && value is Color color)
            return color;
        return Color.FromArgb(fallback);
    }

    private static void ApplySwipeDigitBranding(View view)
    {
        if (view is Label label && !string.IsNullOrEmpty(label.Text))
        {
            if (label.Text == "A") label.Text = "SW";
            else if (label.Text == "AN") label.Text = "SW";
            else if (label.Text.Contains("AveroNova", StringComparison.Ordinal))
                label.Text = label.Text.Replace("AveroNova", "SwipeDigit", StringComparison.Ordinal);
        }

        switch (view)
        {
            case Layout layout:
                foreach (var child in layout.Children.OfType<View>()) ApplySwipeDigitBranding(child);
                break;
            case Border border when border.Content is View content:
                ApplySwipeDigitBranding(content);
                break;
            case ContentView contentView when contentView.Content is View content:
                ApplySwipeDigitBranding(content);
                break;
            case ScrollView scrollView when scrollView.Content is View content:
                ApplySwipeDigitBranding(content);
                break;
            case RefreshView refreshView when refreshView.Content is View content:
                ApplySwipeDigitBranding(content);
                break;
        }
    }

    private sealed record MobileMenuEntry(
        string PermissionKey,
        string Label,
        string Title,
        string Breadcrumb,
        Func<ContentPage> Factory);
}
