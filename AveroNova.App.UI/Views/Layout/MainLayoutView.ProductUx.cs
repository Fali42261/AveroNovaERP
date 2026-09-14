using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using Microsoft.Maui.Controls.Shapes;

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
            ApplySwapDigitBranding(this);
            if (!_productUxConnectivityHooked) { _connectivity.StatusChanged += OnProductUxConnectivityChanged; _productUxConnectivityHooked = true; }
            if (!_productUxNavigationHooked) { _contentNavigator.PageChanged += OnProductUxPageChanged; _productUxNavigationHooked = true; }
            if (!_productUxLoadedHooked) { Loaded += OnProductUxLoaded; _productUxLoadedHooked = true; }
        }
        else
        {
            if (_productUxConnectivityHooked) { _connectivity.StatusChanged -= OnProductUxConnectivityChanged; _productUxConnectivityHooked = false; }
            if (_productUxNavigationHooked) { _contentNavigator.PageChanged -= OnProductUxPageChanged; _productUxNavigationHooked = false; }
            if (_productUxLoadedHooked) { Loaded -= OnProductUxLoaded; _productUxLoadedHooked = false; }
            MBtnSettings.Clicked -= OnMoreClicked;
        }
    }

    private async void OnProductUxLoaded(object? sender, EventArgs e)
    {
        ApplySwapDigitBranding(this);
        BtnSyncCenter.IsVisible = false;
        if (_initialDashboardLoaded) return;
        _initialDashboardLoaded = true;
        try
        {
            var page = _dashboardFactory();
            ShowMobilePage(page, "Dashboard", "Home / Dashboard");
            _contentNavigator.SetRoot(page, "Dashboard", "Home / Dashboard");
            UpdateMobileNavSelection("Dashboard");
            if (page is IHostedPage hosted) await hosted.LoadForHostAsync();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Dashboard] Initial load failed: {ex}"); }
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

    private void OnProductUxConnectivityChanged(object? sender, ConnectivityStatus status) => MainThread.BeginInvokeOnMainThread(HideOfflineBanners);
    private void OnProductUxPageChanged(object? sender, HostedPage entry) => MainThread.BeginInvokeOnMainThread(() => { UpdateMobileNavSelection(entry.Title); ApplySwapDigitBranding(this); BtnSyncCenter.IsVisible = false; });
    private void HideOfflineBanners() { OfflineBanner.IsVisible = false; MOfflineBanner.IsVisible = false; }

    private void UpdateMobileNavSelection(string? title)
    {
        var secondary = ResolveColor("TextSecondary", "#64748B");
        var primary = ResolveColor("PrimaryColor", "#2563EB");
        var buttons = new[] { MBtnDashboard, MBtnBilling, MBtnCustomers, MBtnReports, MBtnSettings };
        foreach (var button in buttons) { button.TextColor = secondary; button.FontAttributes = FontAttributes.None; button.BackgroundColor = Colors.Transparent; }
        Button active = title switch
        {
            "Dashboard" => MBtnDashboard,
            "Billing" or "New Invoice" or "Edit Invoice" or "Invoice Details" => MBtnBilling,
            "Customers" or "Add Customer" or "Edit Customer" or "Customer Details" => MBtnCustomers,
            "Reports" => MBtnReports,
            _ => MBtnSettings
        };
        active.TextColor = primary;
        active.FontAttributes = FontAttributes.Bold;
    }

    private void OnMoreClicked(object? sender, EventArgs e)
    {
        try
        {
            var permissions = _session.Permissions;
            var entries = BuildMobileMoreEntries().Where(x => MenuCatalog.IsAllowed(x.PermissionKey, permissions)).ToList();
            var stack = new VerticalStackLayout { Padding = new Thickness(16, 14), Spacing = 10 };
            stack.Children.Add(new Label { Text = "More", FontSize = 24, FontAttributes = FontAttributes.Bold });
            stack.Children.Add(new Label { Text = "Core business modules", FontSize = 12, TextColor = Color.FromArgb("#64748B"), Margin = new Thickness(0,0,0,8) });

            foreach (var entry in entries)
            {
                var button = new Button
                {
                    Text = entry.Label,
                    HorizontalOptions = LayoutOptions.Fill,
                    HeightRequest = 48,
                    CornerRadius = 10,
                    BackgroundColor = Colors.Transparent,
                    TextColor = ResolveColor("TextPrimary", "#0F172A"),
                    BorderColor = ResolveColor("BorderColor", "#E2E8F0"),
                    BorderWidth = 1,
                    Padding = new Thickness(16,0)
                };
                var captured = entry;
                button.Clicked += async (_,_) => await OpenMobileEntryAsync(captured);
                stack.Children.Add(button);
            }

            var page = new ContentPage { Title = "More", Content = new ScrollView { Content = stack } };
            ShowMobilePage(page, "More", "Home / More");
            _contentNavigator.SetRoot(page, "More", "Home / More");
            UpdateMobileNavSelection("More");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainMenu] More page failed: {ex}");
        }
    }

    private async Task OpenMobileEntryAsync(MobileMenuEntry selected)
    {
        try
        {
            var exemptFromLicense = selected.PermissionKey is "License" or "Settings" or "Help";
            if (!exemptFromLicense)
            {
                var access = await _licenses.GetAccessStateAsync();
                if (!access.AllowsAccess) { await NavigateMobileAsync(() => _licenseFactory(), "Plan", "Home / Plan"); return; }
            }
            await NavigateMobileAsync(selected.Factory, selected.Title, selected.Breadcrumb);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainMenu] {selected.Title} failed: {ex}");
            await ShowMessageAsync(selected.Title, $"{selected.Title} could not be opened. Please try again.");
        }
    }

    // Keep the full module implementations in code. Mobile More intentionally exposes
    // only the modules needed for the simple day-to-day business workflow.
    private List<MobileMenuEntry> BuildMobileMoreEntries() =>
    [
        new("Company", "Company", "Company", "Home / Company", () => _companyFactory()),
        new("Products", "Products", "Products", "Home / Products", () => _productsFactory()),
        new("Inventory", "Inventory", "Inventory", "Home / Inventory", () => _inventoryFactory()),
        new("Purchases", "Purchases", "Purchases", "Home / Purchases", () => _purchasesFactory()),
        new("Payments", "Payments", "Payments", "Home / Payments", () => _paymentsFactory()),
        new("Expenses", "Expenses", "Expenses", "Home / Expenses", () => _expensesFactory()),
        new("Users", "Users", "Users", "Home / Administration / Users", () => _usersFactory()),
        new("Roles", "Roles", "Roles", "Home / Administration / Roles", () => _rolesFactory()),
        new("Settings", "Settings", "Settings", "Home / Settings", () => _settingsFactory()),
        new("License", "Plan", "Plan", "Home / Plan", () => _licenseFactory()),
        new("Help", "Help & Support", "Help & Support", "Home / Help", () => _helpFactory())
    ];

    private async Task NavigateMobileAsync(Func<ContentPage> factory, string title, string breadcrumb)
    {
        ContentPage page;
        try { page = factory(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainMenu] Factory failed for {title}: {ex}"); await ShowMessageAsync(title, $"{title} could not be opened."); return; }
        ShowMobilePage(page, title, breadcrumb);
        _contentNavigator.SetRoot(page, title, breadcrumb);
        UpdateMobileNavSelection(title);
        if (page is IHostedPage hosted)
        {
            try { await hosted.LoadForHostAsync(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainMenu] Hosted load failed for {title}: {ex}"); await ShowMessageAsync(title, $"{title} opened, but its data could not be loaded. Pull down to retry."); }
        }
    }

    private Page GetHostPage()
    {
        var page = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault(w => w.Page is not null)?.Page;
        if (page is not null) return page;
        if (Window?.Page is Page windowPage) return windowPage;
        return Shell.Current ?? throw new InvalidOperationException("No active page is available.");
    }

    private async Task ShowMessageAsync(string title,string message) => await GetHostPage().DisplayAlert(title,message,"OK");
    private static Color ResolveColor(string resourceKey,string fallback) => Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(resourceKey,out var value) == true && value is Color color ? color : Color.FromArgb(fallback);

    private static void ApplySwapDigitBranding(View view)
    {
        if (view is Label label && !string.IsNullOrEmpty(label.Text))
        {
            if (label.Text is "A" or "AN" or "SW") label.Text = "SD";
            else if (label.Text.Contains("AveroNova", StringComparison.Ordinal)) label.Text = label.Text.Replace("AveroNova","SwapDigit",StringComparison.Ordinal);
            else if (label.Text.Contains("SwipeDigit", StringComparison.Ordinal)) label.Text = label.Text.Replace("SwipeDigit","SwapDigit",StringComparison.Ordinal);
        }
        switch (view)
        {
            case Microsoft.Maui.Controls.Layout layout:
                foreach (var child in layout.Children.OfType<View>()) ApplySwapDigitBranding(child);
                break;
            case Border border when border.Content is View content:
                ApplySwapDigitBranding(content);
                break;
            case ContentView contentView when contentView.Content is View content:
                ApplySwapDigitBranding(content);
                break;
            case ScrollView scrollView when scrollView.Content is View content:
                ApplySwapDigitBranding(content);
                break;
            case RefreshView refreshView when refreshView.Content is View content:
                ApplySwapDigitBranding(content);
                break;
        }
    }

    private sealed record MobileMenuEntry(string PermissionKey,string Label,string Title,string Breadcrumb,Func<ContentPage> Factory);
}
