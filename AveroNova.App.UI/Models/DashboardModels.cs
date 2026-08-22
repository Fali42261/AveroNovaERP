namespace AveroNova.App.UI.Models;

public sealed class DashboardSnapshot
{
    public string WelcomeMessage { get; init; } = "Welcome";
    public string CompanyName { get; init; } = "No company";
    public string UserName { get; init; } = string.Empty;
    public string UserRole { get; init; } = string.Empty;
    public string UserInitials { get; init; } = "AN";
    public string CurrentDate { get; init; } = string.Empty;
    public string CurrencySymbol { get; init; } = "₹";
    public decimal TodaySales { get; init; }
    public decimal TodayCollection { get; init; }
    public decimal TodayOutstanding { get; init; }
    public int TotalCustomers { get; init; }
    public int TotalProducts { get; init; }
    public int LowStockCount { get; init; }
    public int TotalInvoices { get; init; }
    public int PendingPaymentCount { get; init; }
    public decimal PendingPaymentAmount { get; init; }
    public int TodayInvoiceCount { get; init; }
    public int TodayPaymentCount { get; init; }
    public decimal WeekSales { get; init; }
    public decimal MonthSales { get; init; }
    public decimal YesterdaySales { get; init; }
    public decimal PreviousWeekSales { get; init; }
    public decimal PreviousMonthSales { get; init; }
    public IReadOnlyList<DashboardTransactionItem> RecentTransactions { get; init; } = [];
    public IReadOnlyList<DashboardLowStockItem> LowStockItems { get; init; } = [];
    public IReadOnlyList<DashboardAlertItem> Alerts { get; init; } = [];
}

public sealed class DashboardTransactionItem
{
    public Guid Id { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string AmountText { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string DateText { get; init; } = string.Empty;
    public InvoiceStatus Status { get; init; }
}

public sealed class DashboardLowStockItem
{
    public Guid Id { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string SKU { get; init; } = string.Empty;
    public int Stock { get; init; }
    public int MinimumStock { get; init; }
    public string StockText => $"{Stock} / {MinimumStock}";
}

public sealed class DashboardAlertItem
{
    public string Title { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public DashboardAlertKind Kind { get; init; }
    public string Destination { get; init; } = string.Empty;
}

public enum DashboardAlertKind
{
    LowStock,
    PendingPayment,
    OverdueInvoice
}
