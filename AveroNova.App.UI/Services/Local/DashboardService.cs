using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Local;

public sealed class DashboardService : IDashboardService
{
    private readonly IBillingService _billing;
    private readonly IPurchaseService _purchase;
    private readonly IProductService _product;
    private readonly ICustomerService _customer;
    private readonly IPaymentService _payment;
    private readonly ICompanyService _company;
    private readonly IAuthenticationService _auth;

    public DashboardService(
        IBillingService billing,
        IPurchaseService purchase,
        IProductService product,
        ICustomerService customer,
        IPaymentService payment,
        ICompanyService company,
        IAuthenticationService auth)
    {
        _billing = billing;
        _purchase = purchase;
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
        {
            return new DashboardSnapshot
            {
                WelcomeMessage = welcome,
                CompanyName = "No company",
                UserName = userName,
                UserRole = user?.Role ?? string.Empty,
                UserInitials = Initials(user),
                CurrentDate = DateTime.Today.ToString("dddd, dd MMMM yyyy"),
                CurrencySymbol = symbol
            };
        }

        var invoicesTask = _billing.GetAllAsync(companyId);
        var purchasesTask = _purchase.GetAllAsync(companyId);
        var productsTask = _product.GetAllAsync(companyId);
        var customersTask = _customer.GetAllAsync(companyId);
        var paymentsTask = _payment.GetAllAsync(companyId);
        await Task.WhenAll(invoicesTask, purchasesTask, productsTask, customersTask, paymentsTask);

        var invoices = invoicesTask.Result;
        var purchases = purchasesTask.Result;
        var products = productsTask.Result;
        var customers = customersTask.Result;
        var payments = paymentsTask.Result;

        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);
        var weekStart = StartOfWeek(today);
        var prevWeekStart = weekStart.AddDays(-7);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var prevMonthStart = monthStart.AddMonths(-1);

        static bool IsPostedSale(InvoiceModel invoice)
            => invoice.Status is InvoiceStatus.Sent or InvoiceStatus.PartialPaid or InvoiceStatus.Paid or InvoiceStatus.Overdue;

        static bool IsPostedPurchase(PurchaseModel purchase)
            => purchase.Status != PurchaseStatus.Draft && purchase.Status != PurchaseStatus.Cancelled;

        var sales = invoices.Where(IsPostedSale).ToList();
        var postedPurchases = purchases.Where(IsPostedPurchase).ToList();

        decimal SalesOn(DateTime day)
            => sales.Where(i => i.InvoiceDate.Date == day).Sum(i => i.GrandTotal);

        decimal SalesBetween(DateTime fromInclusive, DateTime toExclusive)
            => sales.Where(i => i.InvoiceDate.Date >= fromInclusive && i.InvoiceDate.Date < toExclusive).Sum(i => i.GrandTotal);

        decimal PurchasesOn(DateTime day)
            => postedPurchases.Where(p => p.PurchaseDate.Date == day).Sum(p => p.GrandTotal);

        decimal PurchasesBetween(DateTime fromInclusive, DateTime toExclusive)
            => postedPurchases.Where(p => p.PurchaseDate.Date >= fromInclusive && p.PurchaseDate.Date < toExclusive).Sum(p => p.GrandTotal);

        var pending = sales
            .Where(i => i.Status != InvoiceStatus.Paid && i.DueAmount > 0)
            .ToList();

        var overdue = pending
            .Where(i => i.Status == InvoiceStatus.Overdue || i.DueDate.Date < today)
            .ToList();

        var purchasePayables = postedPurchases
            .Where(p => p.DueAmount > 0)
            .ToList();

        var lowStock = products
            .Where(p => p.IsLowStock)
            .OrderBy(p => p.Stock)
            .ThenBy(p => p.Name)
            .Take(10)
            .ToList();

        var todayCustomerPayments = payments
            .Where(p => !p.IsSupplier && p.PaymentDate.Date == today && p.Status == PaymentStatus.Completed)
            .ToList();

        var sevenDayTrend = Enumerable.Range(0, 7)
            .Select(offset => today.AddDays(offset - 6))
            .Select(day => new DashboardTrendPoint
            {
                Date = day,
                Sales = SalesOn(day),
                Purchases = PurchasesOn(day)
            })
            .ToList();

        var recent = invoices
            .Where(i => i.Status != InvoiceStatus.Cancelled)
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.InvoiceNumber)
            .Take(5)
            .Select(i =>
            {
                var effectiveStatus = i.Status == InvoiceStatus.Sent && i.DueAmount > 0 && i.DueDate.Date < today
                    ? InvoiceStatus.Overdue
                    : i.Status;
                return new DashboardTransactionItem
                {
                    Id = i.LocalId,
                    InvoiceNumber = i.InvoiceNumber,
                    CustomerName = string.IsNullOrWhiteSpace(i.CustomerName) ? "—" : i.CustomerName,
                    AmountText = FormatMoney(symbol, i.GrandTotal),
                    StatusLabel = effectiveStatus == InvoiceStatus.Overdue ? "Overdue" : i.StatusLabel,
                    DateText = i.InvoiceDate.ToString("dd MMM yyyy"),
                    Status = effectiveStatus
                };
            })
            .ToList();

        // Top selling products — aggregate posted-sale invoice line items
        var productLookup = products.ToDictionary(p => p.LocalId);
        var topProducts = sales
            .SelectMany(i => i.Items)
            .GroupBy(li => li.ProductId)
            .Select(g =>
            {
                productLookup.TryGetValue(g.Key, out var prod);
                return new DashboardTopProduct
                {
                    ProductId   = g.Key,
                    ProductName = prod?.Name ?? g.First().ProductName,
                    Category    = prod?.Category ?? string.Empty,
                    SoldQty     = g.Sum(x => x.Quantity),
                    Revenue     = g.Sum(x => x.GrandTotal)
                };
            })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToList();

        var lowStockItems = lowStock
            .Select(p => new DashboardLowStockItem
            {
                Id = p.LocalId,
                ProductName = p.Name,
                SKU = p.SKU,
                Stock = p.Stock,
                MinimumStock = p.MinimumStock
            })
            .ToList();

        var alerts = new List<DashboardAlertItem>();
        if (lowStockItems.Count > 0)
        {
            alerts.Add(new DashboardAlertItem
            {
                Title = "Low stock",
                Detail = lowStockItems.Count == 1
                    ? $"{lowStockItems[0].ProductName} is at or below minimum stock."
                    : $"{lowStockItems.Count} products need stock attention.",
                Kind = DashboardAlertKind.LowStock,
                Destination = MainContentNavigator.Inventory
            });
        }

        if (pending.Count > 0)
        {
            alerts.Add(new DashboardAlertItem
            {
                Title = "Pending payments",
                Detail = $"{pending.Count} invoice{(pending.Count == 1 ? "" : "s")} · {FormatMoney(symbol, pending.Sum(i => i.DueAmount))} outstanding.",
                Kind = DashboardAlertKind.PendingPayment,
                Destination = MainContentNavigator.Payments
            });
        }

        if (overdue.Count > 0)
        {
            alerts.Add(new DashboardAlertItem
            {
                Title = "Overdue invoices",
                Detail = $"{overdue.Count} invoice{(overdue.Count == 1 ? "" : "s")} past due date.",
                Kind = DashboardAlertKind.OverdueInvoice,
                Destination = MainContentNavigator.Billing
            });
        }

        return new DashboardSnapshot
        {
            WelcomeMessage = welcome,
            CompanyName = string.IsNullOrWhiteSpace(company?.Name) ? (user?.CompanyName ?? "No company") : company!.Name,
            UserName = userName,
            UserRole = user?.Role ?? string.Empty,
            UserInitials = Initials(user),
            CurrentDate = today.ToString("dddd, dd MMMM yyyy"),
            CurrencySymbol = symbol,

            TodaySales = SalesOn(today),
            TodayPurchases = PurchasesOn(today),
            TodayCollection = todayCustomerPayments.Sum(p => p.Amount),
            TodayOutstanding = pending.Sum(i => i.DueAmount),
            OutstandingPayable = purchasePayables.Sum(p => p.DueAmount),

            TotalCustomers = customers.Count,
            TotalProducts = products.Count,
            LowStockCount = lowStockItems.Count,
            TotalInvoices = invoices.Count,
            PendingPaymentCount = pending.Count,
            PendingPaymentAmount = pending.Sum(i => i.DueAmount),
            TodayInvoiceCount = sales.Count(i => i.InvoiceDate.Date == today),
            TodayPaymentCount = todayCustomerPayments.Count,

            WeekSales = SalesBetween(weekStart, today.AddDays(1)),
            WeekPurchases = PurchasesBetween(weekStart, today.AddDays(1)),
            MonthSales = SalesBetween(monthStart, today.AddDays(1)),
            MonthPurchases = PurchasesBetween(monthStart, today.AddDays(1)),
            YesterdaySales = SalesOn(yesterday),
            PreviousWeekSales = SalesBetween(prevWeekStart, weekStart),
            PreviousMonthSales = SalesBetween(prevMonthStart, monthStart),

            TodayOrderCount = sales.Count(i => i.InvoiceDate.Date == today),
            YesterdayOrderCount = sales.Count(i => i.InvoiceDate.Date == yesterday),

            SevenDayTrend = sevenDayTrend,
            RecentTransactions = recent,
            LowStockItems = lowStockItems,
            Alerts = alerts,
            TopProducts = topProducts
        };
    }

    private static string Initials(UserModel? user)
    {
        if (!string.IsNullOrWhiteSpace(user?.AvatarInitials)) return user.AvatarInitials;
        if (string.IsNullOrWhiteSpace(user?.Name)) return "SW";
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
