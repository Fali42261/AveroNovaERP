using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Dashboard;

public partial class DashboardPage : ContentPage
{
    private readonly IDashboardService _dashboard;
    private readonly ICompanyService _company;
    private readonly IAuthenticationService _auth;
    private readonly ISubscriptionService _subscription;
    private bool _isLoading;

    public DashboardPage(
        IDashboardService dashboard,
        ICompanyService company,
        IAuthenticationService auth,
        ISubscriptionService subscription)
    {
        InitializeComponent();
        _dashboard = dashboard;
        _company = company;
        _auth = auth;
        _subscription = subscription;
        DashboardRoot.Loaded += async (_, _) => await LoadDataAsync();
    }

    public Task ReloadAsync() => LoadDataAsync();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        try { await LoadDataAsync(); }
        finally { Refresher.IsRefreshing = false; }
    }

    private async void OnNewInvoiceClicked(object? sender, EventArgs e) => await NavigateToAsync(AppRoutes.InvoiceNew);
    private async void OnNewCustomerClicked(object? sender, EventArgs e) => await NavigateToAsync(AppRoutes.CustomerAdd);
    private async void OnNewPurchaseClicked(object? sender, EventArgs e) => await NavigateToAsync(AppRoutes.PurchaseNew);
    private async void OnStockAdjustClicked(object? sender, EventArgs e) => await NavigateToAsync(AppRoutes.StockAdjust);
    private void OnViewAllInvoicesClicked(object? sender, EventArgs e) => MainContentNavigator.Request(MainContentNavigator.Billing);
    private void OnViewInventoryClicked(object? sender, EventArgs e) => MainContentNavigator.Request(MainContentNavigator.Inventory);

    private static async Task NavigateToAsync(string route)
    {
        try
        {
            if (Shell.Current != null)
                await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Dashboard navigation failed for {route}: {ex}");
        }
    }

    private static async Task OpenInvoiceAsync(Guid invoiceId)
    {
        if (invoiceId == Guid.Empty || Shell.Current == null) return;
        try { await Shell.Current.GoToAsync($"{AppRoutes.InvoiceView}?id={invoiceId}"); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Invoice navigation failed: {ex}"); }
    }

    private async Task LoadDataAsync()
    {
        if (_isLoading) return;
        _isLoading = true;

        try
        {
            var snapshot = await _dashboard.GetSnapshotAsync();
            LblDate.Text = snapshot.CurrentDate;
            LblWelcome.Text = snapshot.WelcomeMessage;

            var company = _company.CurrentCompany;
            var user = _auth.CurrentUser;
            var companyId = company?.LocalId ?? user?.CompanyId ?? Guid.Empty;
            var subscription = companyId == Guid.Empty ? null : await _subscription.GetCurrentAsync(companyId);
            BuildCompanySection(company, user, subscription);

            if (subscription?.IsExpired == true)
            {
                ResetOperationalSections();
                return;
            }

            var symbol = ResolveCurrencySymbol(snapshot.CurrencySymbol, company?.CurrencySymbol);
            ApplyKpis(snapshot, symbol);
            ApplyPerformance(snapshot, symbol);
            BuildFinancialChart(snapshot, symbol);
            BuildBusinessAlerts(snapshot.Alerts);
            BuildRecentInvoices(snapshot.RecentTransactions);
            BuildLowStock(snapshot.LowStockItems);
        }
        catch (Exception ex)
        {
            InvoiceList.Children.Clear();
            LowStockList.Children.Clear();
            BusinessAlertsHost.Children.Clear();
            BusinessAlertsHost.Children.Add(CreateMessageCard("Unable to load dashboard data.", "#DC2626"));
            System.Diagnostics.Debug.WriteLine($"Dashboard load failed: {ex}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ApplyKpis(DashboardSnapshot snapshot, string symbol)
    {
        LblTotalSales.Text = FormatMoney(symbol, snapshot.MonthSales);
        LblTotalPurchases.Text = FormatMoney(symbol, snapshot.MonthPurchases);
        LblOutstanding.Text = FormatMoney(symbol, snapshot.TodayOutstanding);
        LblCustomers.Text = snapshot.TotalCustomers.ToString();
        LblProducts.Text = snapshot.TotalProducts.ToString();
        LblPayments.Text = FormatMoney(symbol, snapshot.TodayCollection);

        LblSalesChange.Text = snapshot.TotalInvoices == 0 ? "No data" : $"{snapshot.TotalInvoices} invoices";
        LblPurchasesMeta.Text = snapshot.TodayPurchases == 0 ? "No purchases today" : $"{FormatMoney(symbol, snapshot.TodayPurchases)} today";
        LblOutstandingMeta.Text = snapshot.PendingPaymentCount == 0 ? "No dues" : $"{snapshot.PendingPaymentCount} invoices";
        LblCustomersMeta.Text = snapshot.TotalCustomers == 0 ? "No data" : $"{snapshot.TotalCustomers} total";
        LblProductsMeta.Text = snapshot.LowStockCount == 0 ? "Stock healthy" : $"{snapshot.LowStockCount} low stock";
        LblPaymentsMeta.Text = snapshot.TodayPaymentCount == 0 ? "No receipts today" : $"{snapshot.TodayPaymentCount} received";
        LblLowStockCount.Text = snapshot.LowStockCount == 0 ? "All healthy" : $"{snapshot.LowStockCount} item{(snapshot.LowStockCount == 1 ? "" : "s")}";
    }

    private void ApplyPerformance(DashboardSnapshot snapshot, string symbol)
    {
        LblWeekSales.Text = FormatMoney(symbol, snapshot.WeekSales);
        LblMonthSales.Text = FormatMoney(symbol, snapshot.MonthSales);
        LblPayable.Text = FormatMoney(symbol, snapshot.OutstandingPayable);
        LblWeekPurchases.Text = FormatMoney(symbol, snapshot.WeekPurchases);

        ApplyTrendLabel(LblWeekSalesTrend, snapshot.WeekSales, snapshot.PreviousWeekSales, "vs last week");
        ApplyTrendLabel(LblMonthSalesTrend, snapshot.MonthSales, snapshot.PreviousMonthSales, "vs last month");
    }

    private static void ApplyTrendLabel(Label label, decimal current, decimal previous, string suffix)
    {
        if (current == 0 && previous == 0)
        {
            label.Text = "No comparison data";
            label.TextColor = Color.FromArgb("#64748B");
            return;
        }

        if (previous == 0)
        {
            label.Text = current > 0 ? $"New · {suffix}" : "No comparison data";
            label.TextColor = current > 0 ? Color.FromArgb("#059669") : Color.FromArgb("#64748B");
            return;
        }

        var percent = (current - previous) / previous * 100m;
        label.Text = $"{(percent >= 0 ? "+" : string.Empty)}{percent:N0}% {suffix}";
        label.TextColor = percent >= 0 ? Color.FromArgb("#059669") : Color.FromArgb("#DC2626");
    }

    private void BuildFinancialChart(DashboardSnapshot snapshot, string symbol)
    {
        SalesChartHost.Children.Clear();
        SalesChartHost.ColumnDefinitions.Clear();

        var points = snapshot.SevenDayTrend?.ToList() ?? [];
        if (points.Count == 0)
        {
            SalesChartHost.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            SalesChartHost.Add(new Label
            {
                Text = "No chart data available.",
                FontSize = 12,
                TextColor = Color.FromArgb("#64748B"),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);
            LblChartSummary.Text = "Chart will update from invoices and purchases.";
            return;
        }

        var maxValue = points.SelectMany(x => new[] { x.Sales, x.Purchases }).DefaultIfEmpty(0m).Max();
        var salesTotal = points.Sum(x => x.Sales);
        var purchaseTotal = points.Sum(x => x.Purchases);
        LblChartSummary.Text = $"7-day sales {FormatMoney(symbol, salesTotal)} · purchases {FormatMoney(symbol, purchaseTotal)}";

        for (var i = 0; i < points.Count; i++)
        {
            SalesChartHost.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var point = points[i];

            var column = new VerticalStackLayout
            {
                Spacing = 5,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.End
            };

            column.Children.Add(new Label
            {
                Text = FormatCompactMoney(symbol, Math.Max(point.Sales, point.Purchases)),
                FontSize = 9,
                TextColor = Color.FromArgb("#94A3B8"),
                HorizontalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.NoWrap
            });

            var barArea = new Grid { HeightRequest = 120, VerticalOptions = LayoutOptions.End };
            var bars = new HorizontalStackLayout
            {
                Spacing = 4,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End
            };

            var salesBar = CreateChartBar("#2563EB", CalculateBarHeight(point.Sales, maxValue));
            var purchaseBar = CreateChartBar("#7C3AED", CalculateBarHeight(point.Purchases, maxValue));
            ToolTipProperties.SetText(salesBar, $"{point.DateLabel} sales: {FormatMoney(symbol, point.Sales)}");
            ToolTipProperties.SetText(purchaseBar, $"{point.DateLabel} purchases: {FormatMoney(symbol, point.Purchases)}");
            bars.Children.Add(salesBar);
            bars.Children.Add(purchaseBar);
            barArea.Children.Add(bars);
            column.Children.Add(barArea);

            column.Children.Add(new Label
            {
                Text = point.DayLabel,
                FontSize = 10,
                FontAttributes = point.Date.Date == DateTime.Today ? FontAttributes.Bold : FontAttributes.None,
                TextColor = point.Date.Date == DateTime.Today ? Color.FromArgb("#2563EB") : Color.FromArgb("#64748B"),
                HorizontalOptions = LayoutOptions.Center
            });

            SalesChartHost.Add(column, i, 0);
        }
    }

    private static Border CreateChartBar(string color, double height)
        => new()
        {
            WidthRequest = 13,
            HeightRequest = height,
            MinimumHeightRequest = 3,
            BackgroundColor = Color.FromArgb(color),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(4, 4, 1, 1) },
            VerticalOptions = LayoutOptions.End
        };

    private static double CalculateBarHeight(decimal value, decimal maxValue)
    {
        if (value <= 0 || maxValue <= 0) return 3;
        return Math.Max(8, (double)(value / maxValue) * 112d);
    }

    private void BuildBusinessAlerts(IReadOnlyList<DashboardAlertItem> alerts)
    {
        BusinessAlertsHost.Children.Clear();
        LblBusinessAlertCount.Text = alerts.Count == 0 ? "All clear" : $"{alerts.Count} alert{(alerts.Count == 1 ? "" : "s")}";

        if (alerts.Count == 0)
        {
            BusinessAlertsHost.Children.Add(CreateMessageCard("No business alerts right now. Inventory and receivables look clear.", "#059669"));
            return;
        }

        foreach (var alert in alerts)
            BusinessAlertsHost.Children.Add(BuildAlertCard(alert));
    }

    private static View BuildAlertCard(DashboardAlertItem alert)
    {
        var (icon, accent, background) = alert.Kind switch
        {
            DashboardAlertKind.LowStock => ("⚡", "#D97706", "#FFFBEB"),
            DashboardAlertKind.OverdueInvoice => ("⚠", "#DC2626", "#FEF2F2"),
            _ => ("⏳", "#2563EB", "#EFF6FF")
        };

        var card = new Border
        {
            BackgroundColor = Color.FromArgb(background),
            Stroke = Color.FromArgb(accent),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
            Padding = new Thickness(14, 11)
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 10
        };

        grid.Add(new Label { Text = icon, FontSize = 16, TextColor = Color.FromArgb(accent), VerticalOptions = LayoutOptions.Center }, 0, 0);
        var text = new VerticalStackLayout { Spacing = 2 };
        text.Children.Add(new Label { Text = alert.Title, FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(accent) });
        text.Children.Add(new Label { Text = alert.Detail, FontSize = 11, TextColor = Color.FromArgb("#475569"), LineBreakMode = LineBreakMode.WordWrap });
        grid.Add(text, 1, 0);
        grid.Add(new Label { Text = "›", FontSize = 18, TextColor = Color.FromArgb(accent), VerticalOptions = LayoutOptions.Center }, 2, 0);
        card.Content = grid;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(alert.Destination))
                MainContentNavigator.Request(alert.Destination);
        };
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private void BuildRecentInvoices(IReadOnlyList<DashboardTransactionItem> transactions)
    {
        InvoiceList.Children.Clear();
        foreach (var transaction in transactions)
            InvoiceList.Children.Add(BuildInvoiceRow(transaction));

        if (transactions.Count == 0)
        {
            InvoiceList.Children.Add(new Label
            {
                Text = "No recent invoices.",
                FontSize = 13,
                TextColor = Color.FromArgb("#64748B"),
                Padding = new Thickness(18, 14)
            });
        }
    }

    private void BuildLowStock(IReadOnlyList<DashboardLowStockItem> items)
    {
        LowStockList.Children.Clear();
        if (items.Count == 0)
        {
            LowStockList.Children.Add(new Label
            {
                Text = "No low stock items.",
                FontSize = 13,
                TextColor = Color.FromArgb("#64748B"),
                Padding = new Thickness(18, 14)
            });
            return;
        }

        foreach (var item in items)
            LowStockList.Children.Add(BuildLowStockRow(item));
    }

    private void ResetOperationalSections()
    {
        LblTotalSales.Text = "—";
        LblTotalPurchases.Text = "—";
        LblOutstanding.Text = "—";
        LblCustomers.Text = "—";
        LblProducts.Text = "—";
        LblPayments.Text = "—";
        LblWeekSales.Text = "—";
        LblMonthSales.Text = "—";
        LblPayable.Text = "—";
        LblWeekPurchases.Text = "—";
        LblWeekSalesTrend.Text = "Subscription required";
        LblMonthSalesTrend.Text = "Subscription required";
        SalesChartHost.Children.Clear();
        BusinessAlertsHost.Children.Clear();
        InvoiceList.Children.Clear();
        LowStockList.Children.Clear();
        LblChartSummary.Text = "Subscription required to access operational data.";
        LblBusinessAlertCount.Text = string.Empty;
        LblLowStockCount.Text = string.Empty;
    }

    private static View CreateMessageCard(string message, string accent)
    {
        return new Border
        {
            Style = ResolveStyle("AppCard"),
            Padding = new Thickness(16, 13),
            Content = new Label
            {
                Text = message,
                FontSize = 12,
                TextColor = Color.FromArgb(accent),
                LineBreakMode = LineBreakMode.WordWrap
            }
        };
    }

    private static View BuildLowStockRow(DashboardLowStockItem item)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)),
            Padding = new Thickness(18, 12),
            ColumnSpacing = 12,
            BackgroundColor = Colors.Transparent
        };
        var left = new VerticalStackLayout { Spacing = 2 };
        left.Children.Add(new Label { Text = item.ProductName, FontSize = 13, FontAttributes = FontAttributes.Bold });
        if (!string.IsNullOrWhiteSpace(item.SKU))
            left.Children.Add(new Label { Text = $"SKU: {item.SKU}", FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });
        var right = new VerticalStackLayout { Spacing = 2, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = item.Stock.ToString(), FontSize = 14, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.End, TextColor = Color.FromArgb("#DC2626") });
        right.Children.Add(new Label { Text = $"Min {item.MinimumStock}", FontSize = 10, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.End });
        grid.Add(left, 0, 0);
        grid.Add(right, 1, 0);
        var tap = new TapGestureRecognizer { NumberOfTapsRequired = 1 };
        tap.Tapped += (_, _) => MainContentNavigator.Request(MainContentNavigator.Inventory);
        grid.GestureRecognizers.Add(tap);
        return grid;
    }

    private static string ResolveCurrencySymbol(string? snapshotSymbol, string? companySymbol)
        => !string.IsNullOrWhiteSpace(companySymbol)
            ? companySymbol.Trim()
            : !string.IsNullOrWhiteSpace(snapshotSymbol)
                ? snapshotSymbol.Trim()
                : "₹";

    private static string FormatMoney(string symbol, decimal amount) => $"{symbol}{amount:N0}";

    private static string FormatCompactMoney(string symbol, decimal amount)
    {
        var absolute = Math.Abs(amount);
        if (absolute >= 1_000_000m) return $"{symbol}{amount / 1_000_000m:0.#}M";
        if (absolute >= 1_000m) return $"{symbol}{amount / 1_000m:0.#}K";
        return amount == 0 ? "0" : $"{symbol}{amount:0}";
    }

    private void BuildCompanySection(CompanyModel? company, UserModel? user, SubscriptionModel? subscription)
    {
        CompanyDetailsHost.Children.Clear();
        if (subscription?.IsExpired == true)
            CompanyDetailsHost.Children.Add(SubscriptionRestrictionView.Create(SubscriptionMessages.FreeTrialExpired));
        CompanyDetailsHost.Children.Add(BuildCompanyDetailsCard(company, user, subscription));
    }

    private static string FormatSubscription(SubscriptionModel? subscription)
        => subscription == null
            ? "No data"
            : subscription.IsExpired && subscription.IsTrial
                ? SubscriptionMessages.FreeTrialExpired
                : subscription.IsTrial
                    ? $"{subscription.PlanName} — 15-day free trial (ends {subscription.ExpiryDate:dd MMM yyyy})"
                    : $"{subscription.PlanName} — {subscription.StatusLabel}";

    private static View BuildCompanyDetailsCard(CompanyModel? company, UserModel? user, SubscriptionModel? subscription)
    {
        var card = new Border { Style = ResolveStyle("AppCard"), Padding = 18 };
        var stack = new VerticalStackLayout { Spacing = 10 };
        stack.Children.Add(new Label { Text = "Company Details", FontSize = 15, FontAttributes = FontAttributes.Bold });
        if (company == null)
        {
            stack.Children.Add(new Label { Text = "No company data", FontSize = 13, TextColor = Color.FromArgb("#64748B") });
            card.Content = stack;
            return card;
        }

        AddDetail(stack, "Company Name", company.Name);
        AddDetail(stack, "Company Code", company.CompanyCode);
        AddDetail(stack, "Owner", string.IsNullOrWhiteSpace(company.OwnerName) ? user?.Name : company.OwnerName);
        AddDetail(stack, "Mobile", company.Phone);
        AddDetail(stack, "Email", company.Email);
        AddDetail(stack, "Address", FormatAddress(company));
        AddDetail(stack, "GST Number", company.TaxNumber);
        AddDetail(stack, "PAN Number", company.RegistrationNo);
        AddDetail(stack, "Subscription", FormatSubscription(subscription));
        card.Content = stack;
        return card;
    }

    private static void AddDetail(VerticalStackLayout stack, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new GridLength(120)), new ColumnDefinition(GridLength.Star)),
            ColumnSpacing = 8
        };
        row.Add(new Label { Text = label, FontSize = 12, TextColor = Color.FromArgb("#64748B") }, 0, 0);
        row.Add(new Label { Text = value, FontSize = 13, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap }, 1, 0);
        stack.Children.Add(row);
    }

    private static string FormatAddress(CompanyModel company)
        => string.Join(", ", new[] { company.Address, company.City, company.State, company.PinCode, company.Country }.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static Style? ResolveStyle(string key)
        => Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Style style ? style : null;

    private static View BuildInvoiceRow(DashboardTransactionItem item)
    {
        var statusColor = item.Status switch
        {
            InvoiceStatus.Paid => "#059669",
            InvoiceStatus.Overdue => "#DC2626",
            InvoiceStatus.Sent => "#2563EB",
            InvoiceStatus.Draft => "#6B7280",
            InvoiceStatus.Cancelled => "#9CA3AF",
            _ => "#D97706"
        };
        var statusBg = item.Status switch
        {
            InvoiceStatus.Paid => "#ECFDF5",
            InvoiceStatus.Overdue => "#FEF2F2",
            InvoiceStatus.Sent => "#EFF6FF",
            _ => "#F9FAFB"
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)),
            Padding = new Thickness(18, 12),
            ColumnSpacing = 12,
            BackgroundColor = Colors.Transparent
        };
        var left = new VerticalStackLayout { Spacing = 3 };
        left.Children.Add(new Label { Text = item.InvoiceNumber, FontSize = 13, FontAttributes = FontAttributes.Bold });
        left.Children.Add(new Label { Text = item.CustomerName, FontSize = 12, TextColor = Color.FromArgb("#64748B") });
        left.Children.Add(new Label { Text = item.DateText, FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });
        var right = new VerticalStackLayout { Spacing = 3, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = item.AmountText, FontSize = 13, FontAttributes = FontAttributes.Bold });
        var badge = new Border
        {
            BackgroundColor = Color.FromArgb(statusBg),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            Padding = new Thickness(8, 2),
            Content = new Label { Text = item.StatusLabel, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(statusColor) }
        };
        right.Children.Add(badge);
        grid.Add(left, 0, 0);
        grid.Add(right, 1, 0);
        var tap = new TapGestureRecognizer { NumberOfTapsRequired = 1 };
        tap.Tapped += async (_, _) => await OpenInvoiceAsync(item.Id);
        grid.GestureRecognizers.Add(tap);
        return grid;
    }
}
