namespace AveroNova.App.UI.Models;

public sealed record ReportPeriod(DateTime From, DateTime To)
{
    public static ReportPeriod CurrentMonth(DateTime today)
        => new(new DateTime(today.Year, today.Month, 1), today.Date);
}

public sealed class FinancialReportSummary
{
    public decimal GrossSales { get; init; }
    public decimal SalesReturns { get; init; }
    public decimal NetRevenue => GrossSales - SalesReturns;
    public decimal GrossPurchases { get; init; }
    public decimal PurchaseReturns { get; init; }
    public decimal NetPurchases => GrossPurchases - PurchaseReturns;
    public decimal OperatingExpenses { get; init; }
    public decimal NetProfit => NetRevenue - NetPurchases - OperatingExpenses;
    public decimal OutstandingReceivables { get; init; }
    public decimal OutstandingPayables { get; init; }
    public decimal PaymentsReceived { get; init; }
    public decimal PaymentsPaid { get; init; }
    public int InvoiceCount { get; init; }
    public int PurchaseCount { get; init; }
    public int CustomerCount { get; init; }
    public int ActiveCustomerCount { get; init; }
    public int ProductCount { get; init; }
    public int LowStockCount { get; init; }
    public int SupplierCount { get; init; }
    public int OverdueInvoiceCount { get; init; }
}

