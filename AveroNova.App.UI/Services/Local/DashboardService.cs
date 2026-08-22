using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Local;

public sealed class DashboardService : IDashboardService
{
    private readonly IBillingService _billing;
    private readonly IProductService _product;
    private readonly ICustomerService _customer;
    private readonly IPaymentService _payment;
    private readonly ICompanyService _company;
    private readonly IAuthenticationService _auth;

    public DashboardService(IBillingService billing, IProductService product, ICustomerService customer, IPaymentService payment, ICompanyService company, IAuthenticationService auth)
    {
        _billing = billing;
        _product = product;
        _customer = customer;
        _payment = payment;
        _company = company;
        _auth = auth;
    }

    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var user = _auth.CurrentUser;
        var company = _company.CurrentCompany;
        if (company == null && user?.CompanyId is Guid linkedId && linkedId != Guid.Empty)
            company = await _company.GetByIdAsync(linkedId);

        var companyId = company?.LocalId ?? user?.CompanyId ?? Guid.Empty;
        var symbol = string.IsNullOrWhiteSpace(company?.CurrencySymbol) ? "₹" : company!.CurrencySymbol;
        var hour = DateTime.Now.Hour;
        var greeting = hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";
        var userName = user?.Name?.Trim() ?? string.Empty;
        var welcome = string.IsNullOrWhiteSpace(userName) ? greeting : $"{greeting}, {userName}";

        if (companyId == Guid.Empty)
            return new DashboardSnapshot { WelcomeMessage = welcome, CompanyName = "No company", UserName = userName, UserRole = user?.Role ?? string.Empty, UserInitials = Initials(user), CurrentDate = DateTime.Today.ToString("dddd, dd MMMM yyyy"), CurrencySymbol = symbol };

        var invoicesTask = _billing.GetAllAsync(companyId);
        var productsTask = _product.GetAllAsync(companyId);
        var customersTask = _customer.GetAllAsync(companyId);
        var paymentsTask = _payment.GetAllAsync(companyId);
        await Task.WhenAll(invoicesTask, productsTask, customersTask, paymentsTask);

        var invoices = invoicesTask.Result;
        var products = productsTask.Result;
        var customers = customersTask.Result;
        var payments = paymentsTask.Result;
        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);
        var weekStart = StartOfWeek(today);
        var prevWeekStart = weekStart.AddDays(-7);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var prevMonthStart = monthStart.AddMonths(-1);
        static bool IsActiveSale(InvoiceModel i) => i.Status != InvoiceStatus.Cancelled;
        var sales = invoices.Where(IsActiveSale).ToList();
        decimal SalesOn(DateTime day) => sales.Where(i => i.InvoiceDate.Date == day).Sum(i => i.GrandTotal);
        decimal SalesBetween(DateTime fromInclusive, DateTime toExclusive) => sales.Where(i => i.InvoiceDate.Date >= fromInclusive && i.InvoiceDate.Date < toExclusive).Sum(i => i.GrandTotal);
        var pending = invoices.Where(i => i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled).ToList();
        var overdue = pending.Where(i => i.Status == InvoiceStatus.Overdue || i.DueDate.Date < today).ToList();
        var lowStock = products.Where(p => p.IsLowStock).OrderBy(p => p.Stock).ThenBy(p => p.Name).Take(10).ToList();
        var todayPayments = payments.Where(p => p.PaymentDate.Date == today && p.Status == PaymentStatus.Completed).ToList();

        var recent = invoices.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.InvoiceNumber).Take(6).Select(i => new DashboardTransactionItem
        {
            Id = i.LocalId, InvoiceNumber = i.InvoiceNumber, CustomerName = string.IsNullOrWhiteSpace(i.CustomerName) ? "—" : i.CustomerName,
            AmountText = FormatMoney(symbol, i.GrandTotal), StatusLabel = i.StatusLabel, DateText = i.InvoiceDate.ToString("dd MMM yyyy"), Status = i.Status
        }).ToList();

        var lowStockItems = lowStock.Select(p => new DashboardLowStockItem { Id = p.LocalId, ProductName = p.Name, SKU = p.SKU, Stock = p.Stock, MinimumStock = p.MinimumStock }).ToList();
        var alerts = new List<DashboardAlertItem>();
        if (lowStockItems.Count > 0) alerts.Add(new DashboardAlertItem { Title = "Low stock", Detail = lowStockItems.Count == 1 ? $"{lowStockItems[0].ProductName} is at or below minimum stock." : $"{lowStockItems.Count} products need stock attention.", Kind = DashboardAlertKind.LowStock, Destination = MainContentNavigator.Inventory });
        if (pending.Count > 0) alerts.Add(new DashboardAlertItem { Title = "Pending payments", Detail = $"{pending.Count} invoice{(pending.Count == 1 ? "" : "s")} · {FormatMoney(symbol, pending.Sum(i => i.DueAmount))} outstanding.", Kind = DashboardAlertKind.PendingPayment, Destination = MainContentNavigator.Payments });
        if (overdue.Count > 0) alerts.Add(new DashboardAlertItem { Title = "Overdue invoices", Detail = $"{overdue.Count} invoice{(overdue.Count == 1 ? "" : "s")} past due date.", Kind = DashboardAlertKind.OverdueInvoice, Destination = MainContentNavigator.Billing });

        return new DashboardSnapshot
        {
            WelcomeMessage = welcome, CompanyName = string.IsNullOrWhiteSpace(company?.Name) ? (user?.CompanyName ?? "No company") : company!.Name,
            UserName = userName, UserRole = user?.Role ?? string.Empty, UserInitials = Initials(user), CurrentDate = today.ToString("dddd, dd MMMM yyyy"), CurrencySymbol = symbol,
            TodaySales = SalesOn(today), TodayCollection = todayPayments.Sum(p => p.Amount), TodayOutstanding = pending.Sum(i => i.DueAmount), TotalCustomers = customers.Count,
            TotalProducts = products.Count, LowStockCount = lowStockItems.Count, TotalInvoices = invoices.Count, PendingPaymentCount = pending.Count, PendingPaymentAmount = pending.Sum(i => i.DueAmount),
            TodayInvoiceCount = sales.Count(i => i.InvoiceDate.Date == today), TodayPaymentCount = todayPayments.Count, WeekSales = SalesBetween(weekStart, today.AddDays(1)),
            MonthSales = SalesBetween(monthStart, today.AddDays(1)), YesterdaySales = SalesOn(yesterday), PreviousWeekSales = SalesBetween(prevWeekStart, weekStart), PreviousMonthSales = SalesBetween(prevMonthStart, monthStart),
            RecentTransactions = recent, LowStockItems = lowStockItems, Alerts = alerts
        };
    }

    private static string Initials(UserModel? user)
    {
        if (!string.IsNullOrWhiteSpace(user?.AvatarInitials)) return user.AvatarInitials;
        if (string.IsNullOrWhiteSpace(user?.Name)) return "AN";
        var parts = user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
    }

    private static DateTime StartOfWeek(DateTime day)
    {
        var diff = (7 + (day.DayOfWeek - DayOfWeek.Monday)) % 7;
        return day.AddDays(-diff);
    }

    private static string FormatMoney(string symbol, decimal amount) => $"{symbol}{amount:N2}";
}
