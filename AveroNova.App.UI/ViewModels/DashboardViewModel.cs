using System.Collections.ObjectModel;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IDashboardService _dashboard;

    public DashboardViewModel(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isRefreshing;

    [ObservableProperty] private string welcomeMessage = "Welcome";
    [ObservableProperty] private string companyName = "No company";
    [ObservableProperty] private string userName = string.Empty;
    [ObservableProperty] private string userRole = string.Empty;
    [ObservableProperty] private string userInitials = "AN";
    [ObservableProperty] private string currentDate = string.Empty;
    [ObservableProperty] private string userSubtitle = string.Empty;

    [ObservableProperty] private string todaySales = "$0.00";
    [ObservableProperty] private string todaySalesMeta = "No data";
    [ObservableProperty] private string todayCollection = "$0.00";
    [ObservableProperty] private string todayCollectionMeta = "No data";
    [ObservableProperty] private string todayOutstanding = "$0.00";
    [ObservableProperty] private string todayOutstandingMeta = "No data";
    [ObservableProperty] private string totalCustomers = "0";
    [ObservableProperty] private string totalCustomersMeta = "No data";
    [ObservableProperty] private string totalProducts = "0";
    [ObservableProperty] private string totalProductsMeta = "No data";
    [ObservableProperty] private string lowStock = "0";
    [ObservableProperty] private string lowStockMeta = "No data";
    [ObservableProperty] private string totalInvoices = "0";
    [ObservableProperty] private string totalInvoicesMeta = "No data";
    [ObservableProperty] private string pendingPayments = "0";
    [ObservableProperty] private string pendingPaymentsMeta = "No data";

    [ObservableProperty] private string weekSales = "$0.00";
    [ObservableProperty] private string weekSalesTrend = "No data";
    [ObservableProperty] private Color weekSalesTrendColor = Color.FromArgb("#64748B");
    [ObservableProperty] private string monthSales = "$0.00";
    [ObservableProperty] private string monthSalesTrend = "No data";
    [ObservableProperty] private Color monthSalesTrendColor = Color.FromArgb("#64748B");
    [ObservableProperty] private string todayVsYesterday = "No data";
    [ObservableProperty] private Color todayVsYesterdayColor = Color.FromArgb("#64748B");
    [ObservableProperty] private string comparisonSummary = "Sales comparison will appear when invoices have amounts.";

    [ObservableProperty] private bool hasTransactions;
    [ObservableProperty] private bool hasAlerts;

    public ObservableCollection<DashboardTransactionRow> RecentTransactions { get; } = [];
    public ObservableCollection<DashboardAlertRow> Alerts { get; } = [];

    public Task LoadAsync() => LoadCoreAsync(showBusy: !IsRefreshing);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            await LoadCoreAsync(showBusy: false);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task LoadCoreAsync(bool showBusy)
    {
        if (showBusy)
            IsBusy = true;

        try
        {
            var data = await _dashboard.GetSnapshotAsync();
            Apply(data);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Apply(DashboardSnapshot data)
    {
        WelcomeMessage = data.WelcomeMessage;
        CompanyName = data.CompanyName;
        UserName = data.UserName;
        UserRole = data.UserRole;
        UserInitials = data.UserInitials;
        CurrentDate = data.CurrentDate;
        UserSubtitle = string.IsNullOrWhiteSpace(data.UserRole)
            ? data.UserName
            : string.IsNullOrWhiteSpace(data.UserName)
                ? data.UserRole
                : $"{data.UserName} · {data.UserRole}";

        var symbol = data.CurrencySymbol;

        TodaySales = FormatMoney(symbol, data.TodaySales);
        TodaySalesMeta = data.TodayInvoiceCount == 0 ? "No data" : $"{data.TodayInvoiceCount} invoice{(data.TodayInvoiceCount == 1 ? "" : "s")}";
        TodayCollection = FormatMoney(symbol, data.TodayCollection);
        TodayCollectionMeta = data.TodayPaymentCount == 0 ? "No data" : $"{data.TodayPaymentCount} received";
        TodayOutstanding = FormatMoney(symbol, data.TodayOutstanding);
        TodayOutstandingMeta = data.PendingPaymentCount == 0 ? "No data" : $"{data.PendingPaymentCount} open";
        TotalCustomers = data.TotalCustomers.ToString();
        TotalCustomersMeta = data.TotalCustomers == 0 ? "No data" : "Registered";
        TotalProducts = data.TotalProducts.ToString();
        TotalProductsMeta = data.TotalProducts == 0 ? "No data" : "In catalog";
        LowStock = data.LowStockCount.ToString();
        LowStockMeta = data.LowStockCount == 0 ? "None" : "Need attention";
        TotalInvoices = data.TotalInvoices.ToString();
        TotalInvoicesMeta = data.TotalInvoices == 0 ? "No data" : "All time";
        PendingPayments = data.PendingPaymentCount.ToString();
        PendingPaymentsMeta = data.PendingPaymentCount == 0
            ? "No data"
            : FormatMoney(symbol, data.PendingPaymentAmount);

        WeekSales = FormatMoney(symbol, data.WeekSales);
        MonthSales = FormatMoney(symbol, data.MonthSales);

        ApplyTrend(data.WeekSales, data.PreviousWeekSales, "vs last week",
            (text, color) => { WeekSalesTrend = text; WeekSalesTrendColor = color; });
        ApplyTrend(data.MonthSales, data.PreviousMonthSales, "vs last month",
            (text, color) => { MonthSalesTrend = text; MonthSalesTrendColor = color; });
        ApplyTrend(data.TodaySales, data.YesterdaySales, "vs yesterday",
            (text, color) => { TodayVsYesterday = text; TodayVsYesterdayColor = color; });

        ComparisonSummary = BuildComparisonSummary(data);

        RecentTransactions.Clear();
        foreach (var item in data.RecentTransactions)
            RecentTransactions.Add(DashboardTransactionRow.From(item));
        HasTransactions = RecentTransactions.Count > 0;

        Alerts.Clear();
        foreach (var alert in data.Alerts)
            Alerts.Add(DashboardAlertRow.From(alert));
        HasAlerts = Alerts.Count > 0;
    }

    private static string BuildComparisonSummary(DashboardSnapshot data)
    {
        if (data.TotalInvoices == 0)
            return "No sales data yet. Figures update from invoices and payments.";

        var weekDelta = data.WeekSales - data.PreviousWeekSales;
        var monthDelta = data.MonthSales - data.PreviousMonthSales;
        var symbol = data.CurrencySymbol;
        return $"This week {FormatMoney(symbol, data.WeekSales)} ({SignedMoney(symbol, weekDelta)} vs last week). This month {FormatMoney(symbol, data.MonthSales)} ({SignedMoney(symbol, monthDelta)} vs last month).";
    }

    private static string SignedMoney(string symbol, decimal amount)
    {
        var sign = amount > 0 ? "+" : amount < 0 ? "−" : "";
        return $"{sign}{symbol}{Math.Abs(amount):N2}";
    }

    private static void ApplyTrend(decimal current, decimal previous, string suffix, Action<string, Color> apply)
    {
        var muted = Color.FromArgb("#64748B");
        var up = Color.FromArgb("#059669");
        var down = Color.FromArgb("#DC2626");

        if (previous == 0 && current == 0)
        {
            apply("No data", muted);
            return;
        }

        if (previous == 0)
        {
            apply(current > 0 ? $"New · {suffix}" : "No data", current > 0 ? up : muted);
            return;
        }

        var pct = (current - previous) / previous * 100m;
        var sign = pct >= 0 ? "+" : "";
        apply($"{sign}{pct:N0}% {suffix}", pct >= 0 ? up : down);
    }

    private static string FormatMoney(string symbol, decimal amount)
        => $"{symbol}{amount:N2}";

    [RelayCommand]
    private Task CreateInvoiceAsync() => GoAsync(AppRoutes.InvoiceNew);

    [RelayCommand]
    private Task AddCustomerAsync() => GoAsync(AppRoutes.CustomerAdd);

    [RelayCommand]
    private Task AddProductAsync() => GoAsync(AppRoutes.ProductAdd);

    [RelayCommand]
    private Task ReceivePaymentAsync() => GoAsync(AppRoutes.PaymentAdd);

    [RelayCommand]
    private void ViewAllInvoices()
        => MainContentNavigator.Request(MainContentNavigator.Billing);

    [RelayCommand]
    private void ViewCustomers()
        => MainContentNavigator.Request(MainContentNavigator.Customers);

    [RelayCommand]
    private void ViewProducts()
        => MainContentNavigator.Request(MainContentNavigator.Products);

    [RelayCommand]
    private void ViewInventory()
        => MainContentNavigator.Request(MainContentNavigator.Inventory);

    [RelayCommand]
    private void ViewPayments()
        => MainContentNavigator.Request(MainContentNavigator.Payments);

    [RelayCommand]
    private Task OpenInvoiceAsync(DashboardTransactionRow? row)
    {
        if (row == null || row.Id == Guid.Empty)
            return Task.CompletedTask;
        return GoAsync($"{AppRoutes.InvoiceView}?id={row.Id}");
    }

    [RelayCommand]
    private void OpenAlert(DashboardAlertRow? row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.Destination))
            return;
        MainContentNavigator.Request(row.Destination);
    }

    private static Task GoAsync(string route)
    {
        if (Shell.Current is null)
            return Task.CompletedTask;
        return Shell.Current.GoToAsync(route);
    }
}

public sealed class DashboardTransactionRow
{
    public Guid Id { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string AmountText { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string DateText { get; init; } = string.Empty;
    public Color StatusTextColor { get; init; } = Color.FromArgb("#D97706");
    public Color StatusBackground { get; init; } = Color.FromArgb("#FFFBEB");

    public static DashboardTransactionRow From(DashboardTransactionItem item)
    {
        var (text, bg) = item.Status switch
        {
            InvoiceStatus.Paid => (Color.FromArgb("#059669"), Color.FromArgb("#ECFDF5")),
            InvoiceStatus.Overdue => (Color.FromArgb("#DC2626"), Color.FromArgb("#FEF2F2")),
            InvoiceStatus.Sent => (Color.FromArgb("#2563EB"), Color.FromArgb("#EFF6FF")),
            InvoiceStatus.Draft => (Color.FromArgb("#6B7280"), Color.FromArgb("#F9FAFB")),
            InvoiceStatus.Cancelled => (Color.FromArgb("#9CA3AF"), Color.FromArgb("#F9FAFB")),
            _ => (Color.FromArgb("#D97706"), Color.FromArgb("#FFFBEB"))
        };

        return new DashboardTransactionRow
        {
            Id = item.Id,
            InvoiceNumber = item.InvoiceNumber,
            CustomerName = item.CustomerName,
            AmountText = item.AmountText,
            StatusLabel = item.StatusLabel,
            DateText = item.DateText,
            StatusTextColor = text,
            StatusBackground = bg
        };
    }
}

public sealed class DashboardAlertRow
{
    public string Title { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public Color Accent { get; init; }
    public Color Background { get; init; }

    public static DashboardAlertRow From(DashboardAlertItem item)
    {
        var (icon, accent, bg) = item.Kind switch
        {
            DashboardAlertKind.LowStock => ("\u26A1", Color.FromArgb("#D97706"), Color.FromArgb("#FFFBEB")),
            DashboardAlertKind.OverdueInvoice => ("\u26A0", Color.FromArgb("#DC2626"), Color.FromArgb("#FEF2F2")),
            _ => ("\u23F3", Color.FromArgb("#2563EB"), Color.FromArgb("#EFF6FF"))
        };

        return new DashboardAlertRow
        {
            Title = item.Title,
            Detail = item.Detail,
            Destination = item.Destination,
            Icon = icon,
            Accent = accent,
            Background = bg
        };
    }
}
