using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Application.Navigation;
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
    private DashboardSnapshot? _snapshot;
    private DashboardChartPeriod _chartPeriod = DashboardChartPeriod.ThisWeek;

    // ── chart period filter colours ──────────────────────────────────────────
    private static readonly Color ActivePeriodBg   = Color.FromArgb("#2563EB");
    private static readonly Color InactivePeriodBg = Colors.Transparent;
    private static readonly Color ActivePeriodFg   = Colors.White;
    private static readonly Color InactivePeriodFg = Color.FromArgb("#64748B");

    public DashboardPage(
        IDashboardService dashboard,
        ICompanyService company,
        IAuthenticationService auth,
        ISubscriptionService subscription)
    {
        InitializeComponent();
        _dashboard    = dashboard;
        _company      = company;
        _auth         = auth;
        _subscription = subscription;

        DashboardRoot.Loaded += async (_, _) => await LoadDataAsync();
    }

    public Task ReloadAsync() => LoadDataAsync();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
    }

    // ── refresh ──────────────────────────────────────────────────────────────
    private async void OnRefreshing(object? sender, EventArgs e)
    {
        try   { await LoadDataAsync(); }
        finally { Refresher.IsRefreshing = false; }
    }

    // ── chart period selectors ───────────────────────────────────────────────
    private void OnChartPeriodToday(object? sender, TappedEventArgs e)
        => SetChartPeriod(DashboardChartPeriod.Today);

    private void OnChartPeriodWeek(object? sender, TappedEventArgs e)
        => SetChartPeriod(DashboardChartPeriod.ThisWeek);

    private void OnChartPeriodMonth(object? sender, TappedEventArgs e)
        => SetChartPeriod(DashboardChartPeriod.ThisMonth);

    private void SetChartPeriod(DashboardChartPeriod period)
    {
        _chartPeriod = period;
        UpdatePeriodButtonStyles();
        if (_snapshot != null)
            BuildSalesChart(_snapshot, ResolveCurrencySymbol(_snapshot.CurrencySymbol, _company.CurrentCompany?.CurrencySymbol));
    }

    private void UpdatePeriodButtonStyles()
    {
        SetPeriodButton(BtnPeriodToday,  _chartPeriod == DashboardChartPeriod.Today);
        SetPeriodButton(BtnPeriodWeek,   _chartPeriod == DashboardChartPeriod.ThisWeek);
        SetPeriodButton(BtnPeriodMonth,  _chartPeriod == DashboardChartPeriod.ThisMonth);
    }

    private static void SetPeriodButton(Border btn, bool active)
    {
        btn.BackgroundColor = active ? ActivePeriodBg : InactivePeriodBg;
        btn.StrokeThickness = active ? 0 : 1;
        if (btn.Content is Label lbl)
        {
            lbl.TextColor      = active ? ActivePeriodFg : InactivePeriodFg;
            lbl.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
        }
    }

    // ── navigation handlers ──────────────────────────────────────────────────
    private async void OnNewInvoiceClicked(object? sender, EventArgs e)      => await GoAsync(AppRoutes.InvoiceNew);
    private async void OnAddProductClicked(object? sender, EventArgs e)      => await GoAsync(AppRoutes.ProductAdd);
    private async void OnNewCustomerClicked(object? sender, EventArgs e)     => await GoAsync(AppRoutes.CustomerAdd);
    private async void OnNewPurchaseClicked(object? sender, EventArgs e)     => await GoAsync(AppRoutes.PurchaseNew);
    private async void OnRecordPaymentClicked(object? sender, EventArgs e)   => await GoAsync(AppRoutes.PaymentAdd);
    private void OnViewAllInvoicesClicked(object? sender, EventArgs e)       => MainContentNavigator.Request(MainContentNavigator.Billing);
    private void OnViewAllProductsClicked(object? sender, EventArgs e)       => MainContentNavigator.Request(MainContentNavigator.Products);
    private void OnViewInventoryClicked(object? sender, EventArgs e)         => MainContentNavigator.Request(MainContentNavigator.Inventory);
    private void OnViewReportsClicked(object? sender, TappedEventArgs e)     => MainContentNavigator.Request(NavigationMenuCatalog.Reports);

    private static async Task GoAsync(string route)
    {
        if (Shell.Current == null) return;
        try { await Shell.Current.GoToAsync(route); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Dashboard nav failed: {ex}"); }
    }

    private static async Task OpenInvoiceAsync(Guid id)
    {
        if (id == Guid.Empty || Shell.Current == null) return;
        try { await Shell.Current.GoToAsync($"{AppRoutes.InvoiceView}?id={id}"); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Invoice nav failed: {ex}"); }
    }

    // ── main load ────────────────────────────────────────────────────────────
    private async Task LoadDataAsync()
    {
        if (_isLoading) return;
        _isLoading = true;
        LoadingCard.IsVisible = true;

        try
        {
            var snapshot = await _dashboard.GetSnapshotAsync();
            _snapshot = snapshot;

            var company  = _company.CurrentCompany;
            var user     = _auth.CurrentUser;
            var cid      = company?.LocalId ?? user?.CompanyId ?? Guid.Empty;
            var sub      = cid == Guid.Empty ? null : await _subscription.GetCurrentAsync(cid);

            BuildCompanySection(company, user, sub);

            if (sub?.IsExpired == true)
            {
                ResetOperationalSections();
                return;
            }

            var symbol = ResolveCurrencySymbol(snapshot.CurrencySymbol, company?.CurrencySymbol);

            ApplyKpiCards(snapshot, symbol);
            UpdatePeriodButtonStyles();
            BuildSalesChart(snapshot, symbol);
            BuildRecentInvoices(snapshot.RecentTransactions);
            BuildTopProducts(snapshot.TopProducts, symbol);
            ApplyPerformanceSummary(snapshot, symbol);
            BuildBusinessAlerts(snapshot.Alerts);
            BuildLowStock(snapshot.LowStockItems);
        }
        catch (Exception ex)
        {
            ClearSections();
            BusinessAlertsHost.Children.Add(CreateMessageCard("Unable to load dashboard data.", "#DC2626"));
            System.Diagnostics.Debug.WriteLine($"Dashboard load failed: {ex}");
        }
        finally
        {
            LoadingCard.IsVisible = false;
            _isLoading = false;
        }
    }

    // ── KPI cards ────────────────────────────────────────────────────────────
    private void ApplyKpiCards(DashboardSnapshot s, string sym)
    {
        // Today's Sales
        LblTodaySales.Text = FormatMoney(sym, s.TodaySales);
        ApplyTrendPair(LblTodaySalesTrend, LblTodaySalesMeta,
            s.TodaySales, s.YesterdaySales, "vs yesterday");

        // Today's Orders
        LblTodayOrders.Text = s.TodayOrderCount.ToString();
        ApplyCountTrendPair(LblTodayOrdersTrend, LblTodayOrdersMeta,
            s.TodayOrderCount, s.YesterdayOrderCount, "vs yesterday");

        // Total Customers
        LblCustomers.Text = s.TotalCustomers.ToString();
        LblCustomersTrend.Text = string.Empty;
        LblCustomersMeta.Text  = s.TotalCustomers == 0 ? "No data" : "vs last month";
        LblCustomersMeta.TextColor = Color.FromArgb("#64748B");

        // Total Products
        LblProducts.Text = s.TotalProducts.ToString();
        LblProductsTrend.Text = string.Empty;
        LblProductsMeta.Text  = s.LowStockCount == 0 ? "All in stock" : $"{s.LowStockCount} low stock";
        LblProductsMeta.TextColor = s.LowStockCount == 0
            ? Color.FromArgb("#059669")
            : Color.FromArgb("#D97706");
    }

    private static void ApplyTrendPair(Label trendLbl, Label metaLbl,
        decimal current, decimal previous, string suffix)
    {
        if (previous == 0 && current == 0)
        {
            trendLbl.Text      = string.Empty;
            metaLbl.Text       = "No data";
            metaLbl.TextColor  = Color.FromArgb("#64748B");
            return;
        }
        if (previous == 0)
        {
            trendLbl.Text      = current > 0 ? "↑ New" : string.Empty;
            trendLbl.TextColor = Color.FromArgb("#059669");
            metaLbl.Text       = suffix;
            metaLbl.TextColor  = Color.FromArgb("#64748B");
            return;
        }
        var pct = (current - previous) / Math.Abs(previous) * 100m;
        trendLbl.Text      = $"{(pct >= 0 ? "↑" : "↓")} {Math.Abs(pct):N1}%";
        trendLbl.TextColor = pct >= 0 ? Color.FromArgb("#059669") : Color.FromArgb("#DC2626");
        metaLbl.Text       = suffix;
        metaLbl.TextColor  = Color.FromArgb("#64748B");
    }

    private static void ApplyCountTrendPair(Label trendLbl, Label metaLbl,
        int current, int previous, string suffix)
    {
        if (previous == 0 && current == 0)
        {
            trendLbl.Text      = string.Empty;
            metaLbl.Text       = "No orders today";
            metaLbl.TextColor  = Color.FromArgb("#64748B");
            return;
        }
        if (previous == 0)
        {
            trendLbl.Text      = current > 0 ? "↑ New" : string.Empty;
            trendLbl.TextColor = Color.FromArgb("#059669");
            metaLbl.Text       = suffix;
            metaLbl.TextColor  = Color.FromArgb("#64748B");
            return;
        }
        var pct = (current - previous) / (decimal)Math.Abs(previous) * 100m;
        trendLbl.Text      = $"{(pct >= 0 ? "↑" : "↓")} {Math.Abs(pct):N1}%";
        trendLbl.TextColor = pct >= 0 ? Color.FromArgb("#059669") : Color.FromArgb("#DC2626");
        metaLbl.Text       = suffix;
        metaLbl.TextColor  = Color.FromArgb("#64748B");
    }

    // ── sales chart ──────────────────────────────────────────────────────────
    private void BuildSalesChart(DashboardSnapshot snapshot, string symbol)
    {
        SalesChartHost.Children.Clear();
        SalesChartHost.ColumnDefinitions.Clear();

        var today      = DateTime.Today;
        var weekStart  = StartOfWeek(today);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        // Select data points for the chosen period
        List<(string Label, decimal Sales, bool IsToday)> points;

        switch (_chartPeriod)
        {
            case DashboardChartPeriod.Today:
            {
                // single bar for today
                points = [(today.ToString("ddd"), snapshot.TodaySales, true)];
                LblChartLegend.Text = $"Today · {FormatMoney(symbol, snapshot.TodaySales)}";
                break;
            }
            case DashboardChartPeriod.ThisMonth:
            {
                // one point per day this month up to today
                var allDays = snapshot.SevenDayTrend ?? [];
                // build from first of month; use 7-day trend for any matching dates, 0 otherwise
                var trendMap = (snapshot.SevenDayTrend ?? []).ToDictionary(p => p.Date.Date);
                var daysInMonth = (today - monthStart).Days + 1;
                points = Enumerable.Range(0, daysInMonth)
                    .Select(i =>
                    {
                        var d = monthStart.AddDays(i);
                        trendMap.TryGetValue(d, out var tp);
                        return (d.ToString("dd"), tp?.Sales ?? 0m, d.Date == today);
                    }).ToList();
                LblChartLegend.Text = $"This Month · {FormatMoney(symbol, snapshot.MonthSales)}";
                break;
            }
            default: // ThisWeek
            {
                var trendPoints = snapshot.SevenDayTrend ?? [];
                // ensure exactly Mon–Sun of current week
                points = Enumerable.Range(0, 7)
                    .Select(i =>
                    {
                        var d = weekStart.AddDays(i);
                        var tp = trendPoints.FirstOrDefault(p => p.Date.Date == d);
                        return (d.ToString("ddd"), tp?.Sales ?? 0m, d.Date == today);
                    }).ToList();
                LblChartLegend.Text = $"This Week · {FormatMoney(symbol, snapshot.WeekSales)}";
                break;
            }
        }

        if (points.Count == 0 || points.All(p => p.Sales == 0))
        {
            SalesChartHost.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            SalesChartHost.Add(new Label
            {
                Text              = "No sales data for this period.",
                FontSize          = 12,
                TextColor         = Color.FromArgb("#64748B"),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions   = LayoutOptions.Center
            }, 0, 0);
            return;
        }

        var maxVal = points.Select(p => p.Sales).DefaultIfEmpty(0m).Max();

        // For many points (month view) collapse labels by showing every 5th
        var showEveryN = points.Count > 15 ? 5 : 1;

        for (var i = 0; i < points.Count; i++)
        {
            SalesChartHost.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var (label, sales, isToday) = points[i];

            var col = new VerticalStackLayout
            {
                Spacing           = 3,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions   = LayoutOptions.End
            };

            // Value label on bar tip (only if has value)
            if (sales > 0)
            {
                col.Children.Add(new Label
                {
                    Text              = FormatCompactMoney(symbol, sales),
                    FontSize          = 8,
                    TextColor         = Color.FromArgb("#94A3B8"),
                    HorizontalOptions = LayoutOptions.Center,
                    LineBreakMode     = LineBreakMode.NoWrap
                });
            }

            // Bar
            var barHeight = CalculateBarHeight(sales, maxVal, 160);
            var bar = new Border
            {
                WidthRequest          = points.Count > 10 ? 6 : 14,
                HeightRequest         = barHeight,
                MinimumHeightRequest  = sales > 0 ? 4 : 1,
                BackgroundColor       = isToday ? Color.FromArgb("#1D4ED8") : Color.FromArgb("#2563EB"),
                StrokeThickness       = 0,
                StrokeShape           = new RoundRectangle { CornerRadius = new CornerRadius(4, 4, 1, 1) },
                VerticalOptions       = LayoutOptions.End,
                HorizontalOptions     = LayoutOptions.Center
            };

            // Tooltip
            ToolTipProperties.SetText(bar, $"{label}: {FormatMoney(symbol, sales)}");

            var barWrapper = new Grid { HeightRequest = 170, VerticalOptions = LayoutOptions.End };
            barWrapper.Children.Add(bar);
            col.Children.Add(barWrapper);

            // Day label
            var showLabel = points.Count <= 10 || (i % showEveryN == 0);
            col.Children.Add(new Label
            {
                Text              = showLabel ? label : string.Empty,
                FontSize          = 9,
                FontAttributes    = isToday ? FontAttributes.Bold : FontAttributes.None,
                TextColor         = isToday ? Color.FromArgb("#2563EB") : Color.FromArgb("#94A3B8"),
                HorizontalOptions = LayoutOptions.Center
            });

            SalesChartHost.Add(col, i, 0);
        }
    }

    private static double CalculateBarHeight(decimal value, decimal maxValue, double maxPx)
    {
        if (value <= 0 || maxValue <= 0) return 1;
        return Math.Max(4, (double)(value / maxValue) * maxPx);
    }

    // ── recent invoices ──────────────────────────────────────────────────────
    private void BuildRecentInvoices(IReadOnlyList<DashboardTransactionItem> transactions)
    {
        InvoiceList.Children.Clear();

        if (transactions.Count == 0)
        {
            InvoiceList.Children.Add(new Label
            {
                Text      = "No recent invoices.",
                FontSize  = 12,
                TextColor = Color.FromArgb("#64748B"),
                Padding   = new Thickness(0, 10)
            });
            return;
        }

        var isFirst = true;
        foreach (var item in transactions)
        {
            if (!isFirst)
                InvoiceList.Children.Add(new BoxView
                {
                    HeightRequest   = 1,
                    Color           = Color.FromArgb("#F1F5F9"),
                    Margin          = new Thickness(0)
                });
            InvoiceList.Children.Add(BuildInvoiceRow(item));
            isFirst = false;
        }
    }

    private static View BuildInvoiceRow(DashboardTransactionItem item)
    {
        var (statusFg, statusBg) = item.Status switch
        {
            InvoiceStatus.Paid        => ("#059669", "#ECFDF5"),
            InvoiceStatus.Overdue     => ("#DC2626", "#FEF2F2"),
            InvoiceStatus.Sent        => ("#2563EB", "#EFF6FF"),
            InvoiceStatus.Draft       => ("#6B7280", "#F9FAFB"),
            InvoiceStatus.Cancelled   => ("#9CA3AF", "#F9FAFB"),
            _                         => ("#D97706", "#FFFBEB")
        };

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(new GridLength(110)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(80)),
                new ColumnDefinition(new GridLength(70)),
                new ColumnDefinition(new GridLength(64))),
            ColumnSpacing     = 8,
            Padding           = new Thickness(0, 8),
            BackgroundColor   = Colors.Transparent
        };

        // Invoice number — blue link style
        row.Add(new Label
        {
            Text              = item.InvoiceNumber,
            FontSize          = 12,
            TextColor         = Color.FromArgb("#2563EB"),
            VerticalOptions   = LayoutOptions.Center,
            LineBreakMode     = LineBreakMode.TailTruncation
        }, 0, 0);

        row.Add(new Label
        {
            Text              = item.CustomerName,
            FontSize          = 12,
            TextColor         = Color.FromArgb("#1E293B"),
            VerticalOptions   = LayoutOptions.Center,
            LineBreakMode     = LineBreakMode.TailTruncation
        }, 1, 0);

        row.Add(new Label
        {
            Text              = item.DateText,
            FontSize          = 11,
            TextColor         = Color.FromArgb("#64748B"),
            VerticalOptions   = LayoutOptions.Center,
            LineBreakMode     = LineBreakMode.NoWrap
        }, 2, 0);

        row.Add(new Label
        {
            Text              = item.AmountText,
            FontSize          = 12,
            FontAttributes    = FontAttributes.Bold,
            TextColor         = Color.FromArgb("#1E293B"),
            VerticalOptions   = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Start,
            LineBreakMode     = LineBreakMode.NoWrap
        }, 3, 0);

        // Status badge
        var badge = new Border
        {
            BackgroundColor = Color.FromArgb(statusBg),
            StrokeThickness = 0,
            StrokeShape     = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            Padding         = new Thickness(8, 2),
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions   = LayoutOptions.Center,
            Content = new Label
            {
                Text           = item.StatusLabel,
                FontSize       = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor      = Color.FromArgb(statusFg)
            }
        };
        row.Add(badge, 4, 0);

        // Tap to open invoice
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await OpenInvoiceAsync(item.Id);
        row.GestureRecognizers.Add(tap);
        return row;
    }

    // ── top selling products ─────────────────────────────────────────────────
    private void BuildTopProducts(IReadOnlyList<DashboardTopProduct> products, string symbol)
    {
        TopProductsList.Children.Clear();

        if (products.Count == 0)
        {
            TopProductsList.Children.Add(new Label
            {
                Text      = "No sales data yet.",
                FontSize  = 12,
                TextColor = Color.FromArgb("#64748B"),
                Padding   = new Thickness(0, 10)
            });
            return;
        }

        // Colour palette for product icons
        string[] iconColors = ["#3B82F6", "#10B981", "#F59E0B", "#8B5CF6", "#EF4444"];

        var isFirst = true;
        for (var idx = 0; idx < products.Count; idx++)
        {
            var prod = products[idx];
            if (!isFirst)
                TopProductsList.Children.Add(new BoxView
                {
                    HeightRequest = 1,
                    Color         = Color.FromArgb("#F1F5F9")
                });

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection(
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(100)),
                    new ColumnDefinition(new GridLength(70)),
                    new ColumnDefinition(new GridLength(90))),
                ColumnSpacing   = 8,
                Padding         = new Thickness(0, 7),
                BackgroundColor = Colors.Transparent
            };

            // Product name + icon dot
            var nameStack = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
            var dot = new BoxView
            {
                WidthRequest  = 10,
                HeightRequest = 10,
                CornerRadius  = 5,
                Color         = Color.FromArgb(iconColors[idx % iconColors.Length]),
                VerticalOptions = LayoutOptions.Center
            };
            nameStack.Children.Add(dot);
            nameStack.Children.Add(new Label
            {
                Text              = prod.ProductName,
                FontSize          = 12,
                TextColor         = Color.FromArgb("#1E293B"),
                VerticalOptions   = LayoutOptions.Center,
                LineBreakMode     = LineBreakMode.TailTruncation
            });
            row.Add(nameStack, 0, 0);

            row.Add(new Label
            {
                Text              = string.IsNullOrWhiteSpace(prod.Category) ? "—" : prod.Category,
                FontSize          = 12,
                TextColor         = Color.FromArgb("#64748B"),
                VerticalOptions   = LayoutOptions.Center,
                LineBreakMode     = LineBreakMode.TailTruncation
            }, 1, 0);

            row.Add(new Label
            {
                Text              = prod.SoldQty.ToString(),
                FontSize          = 12,
                TextColor         = Color.FromArgb("#1E293B"),
                VerticalOptions   = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Start
            }, 2, 0);

            row.Add(new Label
            {
                Text              = FormatMoney(symbol, prod.Revenue),
                FontSize          = 12,
                FontAttributes    = FontAttributes.Bold,
                TextColor         = Color.FromArgb("#059669"),
                VerticalOptions   = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Start
            }, 3, 0);

            TopProductsList.Children.Add(row);
            isFirst = false;
        }
    }

    // ── performance summary ──────────────────────────────────────────────────
    private void ApplyPerformanceSummary(DashboardSnapshot s, string sym)
    {
        LblWeekSales.Text   = FormatMoney(sym, s.WeekSales);
        LblMonthSales.Text  = FormatMoney(sym, s.MonthSales);
        LblOutstanding.Text = FormatMoney(sym, s.TodayOutstanding);
        LblPayable.Text     = FormatMoney(sym, s.OutstandingPayable);

        ApplyTrendLabel(LblWeekSalesTrend, s.WeekSales,  s.PreviousWeekSales,  "vs last week");
        ApplyTrendLabel(LblMonthSalesTrend, s.MonthSales, s.PreviousMonthSales, "vs last month");

        LblOutstandingMeta.Text = s.PendingPaymentCount == 0
            ? "No open invoices"
            : $"{s.PendingPaymentCount} open invoice{(s.PendingPaymentCount == 1 ? "" : "s")}";
    }

    private static void ApplyTrendLabel(Label lbl, decimal current, decimal previous, string suffix)
    {
        if (current == 0 && previous == 0)
        {
            lbl.Text      = "No data";
            lbl.TextColor = Color.FromArgb("#64748B");
            return;
        }
        if (previous == 0)
        {
            lbl.Text      = current > 0 ? $"New · {suffix}" : "No comparison data";
            lbl.TextColor = current > 0 ? Color.FromArgb("#059669") : Color.FromArgb("#64748B");
            return;
        }
        var pct = (current - previous) / Math.Abs(previous) * 100m;
        lbl.Text      = $"{(pct >= 0 ? "↑ +" : "↓ ")}{Math.Abs(pct):N0}% {suffix}";
        lbl.TextColor = pct >= 0 ? Color.FromArgb("#059669") : Color.FromArgb("#DC2626");
    }

    // ── business alerts ──────────────────────────────────────────────────────
    private void BuildBusinessAlerts(IReadOnlyList<DashboardAlertItem> alerts)
    {
        BusinessAlertsHost.Children.Clear();
        LblBusinessAlertCount.Text = alerts.Count == 0
            ? "All clear"
            : $"{alerts.Count} alert{(alerts.Count == 1 ? "" : "s")}";

        if (alerts.Count == 0)
        {
            BusinessAlertsHost.Children.Add(
                CreateMessageCard("No alerts — inventory and receivables look clear.", "#059669"));
            return;
        }

        foreach (var alert in alerts)
            BusinessAlertsHost.Children.Add(BuildAlertCard(alert));
    }

    private static View BuildAlertCard(DashboardAlertItem alert)
    {
        var (icon, accent, bg) = alert.Kind switch
        {
            DashboardAlertKind.LowStock       => ("⚡", "#D97706", "#FFFBEB"),
            DashboardAlertKind.OverdueInvoice => ("⚠",  "#DC2626", "#FEF2F2"),
            _                                 => ("⏳", "#2563EB", "#EFF6FF")
        };

        var card = new Border
        {
            BackgroundColor = Color.FromArgb(bg),
            Stroke          = Color.FromArgb(accent),
            StrokeThickness = 1,
            StrokeShape     = new RoundRectangle { CornerRadius = new CornerRadius(10) },
            Padding         = new Thickness(14, 10)
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
        text.Children.Add(new Label { Text = alert.Title,  FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(accent) });
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

    // ── low stock ────────────────────────────────────────────────────────────
    private void BuildLowStock(IReadOnlyList<DashboardLowStockItem> items)
    {
        LowStockList.Children.Clear();

        if (items.Count == 0)
        {
            LowStockList.Children.Add(new Label
            {
                Text    = "No low stock items.",
                FontSize = 12,
                TextColor = Color.FromArgb("#64748B"),
                Padding = new Thickness(18, 14)
            });
            return;
        }

        foreach (var item in items)
            LowStockList.Children.Add(BuildLowStockRow(item));
    }

    private static View BuildLowStockRow(DashboardLowStockItem item)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            Padding         = new Thickness(18, 11),
            ColumnSpacing   = 12,
            BackgroundColor = Colors.Transparent
        };

        var left = new VerticalStackLayout { Spacing = 2 };
        left.Children.Add(new Label { Text = item.ProductName, FontSize = 13, FontAttributes = FontAttributes.Bold });
        if (!string.IsNullOrWhiteSpace(item.SKU))
            left.Children.Add(new Label { Text = $"SKU: {item.SKU}", FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });

        var right = new VerticalStackLayout { Spacing = 2, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = item.Stock.ToString(), FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#DC2626"), HorizontalOptions = LayoutOptions.End });
        right.Children.Add(new Label { Text = $"Min {item.MinimumStock}", FontSize = 10, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.End });

        grid.Add(left, 0, 0);
        grid.Add(right, 1, 0);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => MainContentNavigator.Request(MainContentNavigator.Inventory);
        grid.GestureRecognizers.Add(tap);
        return grid;
    }

    // ── subscription / company banner ────────────────────────────────────────
    private void BuildCompanySection(CompanyModel? company, UserModel? user, SubscriptionModel? subscription)
    {
        CompanyDetailsHost.Children.Clear();
        if (subscription?.IsExpired == true)
            CompanyDetailsHost.Children.Add(
                SubscriptionRestrictionView.Create(SubscriptionMessages.FreeTrialExpired));
    }

    // ── reset when subscription expired ─────────────────────────────────────
    private void ResetOperationalSections()
    {
        LblTodaySales.Text   = "—";
        LblTodayOrders.Text  = "—";
        LblCustomers.Text    = "—";
        LblProducts.Text     = "—";
        LblWeekSales.Text    = "—";
        LblMonthSales.Text   = "—";
        LblOutstanding.Text  = "—";
        LblPayable.Text      = "—";
        SalesChartHost.Children.Clear();
        SalesChartHost.ColumnDefinitions.Clear();
        SalesChartHost.Add(new Label
        {
            Text = "Subscription required to view chart data.",
            FontSize = 12, TextColor = Color.FromArgb("#64748B"),
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        InvoiceList.Children.Clear();
        TopProductsList.Children.Clear();
        BusinessAlertsHost.Children.Clear();
        LowStockList.Children.Clear();
    }

    private void ClearSections()
    {
        InvoiceList.Children.Clear();
        TopProductsList.Children.Clear();
        BusinessAlertsHost.Children.Clear();
        LowStockList.Children.Clear();
    }

    // ── helpers ──────────────────────────────────────────────────────────────
    private static View CreateMessageCard(string message, string accent)
        => new Border
        {
            Style   = ResolveStyle("AppCard"),
            Padding = new Thickness(16, 12),
            Content = new Label
            {
                Text          = message,
                FontSize      = 12,
                TextColor     = Color.FromArgb(accent),
                LineBreakMode = LineBreakMode.WordWrap
            }
        };

    private static string ResolveCurrencySymbol(string? snapshotSym, string? companySym)
        => !string.IsNullOrWhiteSpace(companySym)   ? companySym.Trim()
         : !string.IsNullOrWhiteSpace(snapshotSym)  ? snapshotSym.Trim()
         : "₹";

    private static string FormatMoney(string sym, decimal amount)
        => $"{sym}{amount:N0}";

    private static string FormatCompactMoney(string sym, decimal amount)
    {
        var abs = Math.Abs(amount);
        if (abs >= 1_000_000m) return $"{sym}{amount / 1_000_000m:0.#}M";
        if (abs >= 1_000m)     return $"{sym}{amount / 1_000m:0.#}K";
        return amount == 0 ? "0" : $"{sym}{amount:0}";
    }

    private static DateTime StartOfWeek(DateTime day)
    {
        var diff = (7 + (day.DayOfWeek - DayOfWeek.Monday)) % 7;
        return day.AddDays(-diff);
    }

    private static Style? ResolveStyle(string key)
        => Microsoft.Maui.Controls.Application.Current?.Resources
               .TryGetValue(key, out var v) == true && v is Style s ? s : null;
}
