using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Pages.Reports;

public partial class ReportsPage : ContentPage
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICompanyService _company;

    public ReportsPage(IDbContextFactory<AppDbContext> dbFactory, ICompanyService company)
    {
        InitializeComponent();
        _dbFactory = dbFactory;
        _company = company;
        Root.Loaded += async (_, _) => await LoadAsync();
    }

    public Task ReloadAsync() => LoadAsync();
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async void OnRefreshing(object sender, EventArgs e) { await LoadAsync(); Refresher.IsRefreshing = false; }
    private async void OnRefreshClicked(object sender, EventArgs e) => await LoadAsync();

    private async Task LoadAsync()
    {
        try
        {
            var companyId = _company.CurrentCompany?.LocalId ?? Guid.Empty;
            if (companyId == Guid.Empty) { ResetValues(); return; }

            await using var db = await _dbFactory.CreateDbContextAsync();
            var invoices = await db.Invoices.AsNoTracking().Include(x => x.Items)
                .Where(x => x.CompanyId == companyId && !x.IsDeleted)
                .ToListAsync();
            var purchases = await db.Purchases.AsNoTracking().Include(x => x.Items)
                .Where(x => x.CompanyId == companyId && !x.IsDeleted)
                .ToListAsync();
            var payments = await db.Payments.AsNoTracking()
                .Where(x => x.CompanyId == companyId && !x.IsDeleted)
                .ToListAsync();
            var products = await db.Products.AsNoTracking()
                .Where(x => x.CompanyId == companyId && !x.IsDeleted)
                .ToListAsync();
            var movements = await db.StockMovements.AsNoTracking()
                .Where(x => x.CompanyId == companyId && !x.IsDeleted)
                .ToListAsync();

            var activeSales = invoices.Where(x => x.Status != (int)InvoiceStatus.Cancelled && x.Status != (int)InvoiceStatus.Draft).ToList();
            var activePurchases = purchases.Where(x => x.Status != (int)PurchaseStatus.Cancelled).ToList();
            var completedPayments = payments.Where(x => x.Status == (int)PaymentStatus.Completed).ToList();

            var revenue = activeSales.Sum(InvoiceTotal);
            var purchaseTotal = activePurchases.Sum(PurchaseTotal);
            var receivable = activeSales.Sum(x => Math.Max(0m, InvoiceTotal(x) - x.PaidAmount));
            var payable = activePurchases.Sum(x => Math.Max(0m, PurchaseTotal(x) - x.PaidAmount));
            var customerCollections = completedPayments.Where(x => !x.IsSupplier).Sum(x => x.Amount);
            var supplierPayments = completedPayments.Where(x => x.IsSupplier).Sum(x => x.Amount);
            var stockCostValue = products.Sum(x => x.PurchasePrice * Math.Max(0, x.Stock));
            var stockRetailValue = products.Sum(x => x.SellingPrice * Math.Max(0, x.Stock));
            var lowStock = products.Where(x => x.Stock <= x.MinimumStock).OrderBy(x => x.Stock - x.MinimumStock).ToList();
            var overdue = activeSales.Where(x => x.PaidAmount < InvoiceTotal(x) && x.DueDate.Date < DateTime.Today).ToList();
            var receivedPurchases = activePurchases.Where(x => x.Status == (int)PurchaseStatus.Received).ToList();

            RevenueValue.Text = FormatMoney(revenue);
            PurchaseValue.Text = FormatMoney(purchaseTotal);
            ProfitValue.Text = FormatMoney(revenue - purchaseTotal);
            OutstandingValue.Text = FormatMoney(receivable);

            ReportsList.Children.Clear();
            AddReportCard("Sales Report", $"{activeSales.Count} posted sale(s) · {FormatMoney(revenue)} sales · {FormatMoney(receivable)} receivable · {overdue.Count} overdue");
            AddReportCard("Purchase Report", $"{activePurchases.Count} purchase(s) · {receivedPurchases.Count} received · {FormatMoney(purchaseTotal)} total · {FormatMoney(payable)} payable");
            AddReportCard("Payment Report", $"{completedPayments.Count} completed payment(s) · {FormatMoney(customerCollections)} customer receipts · {FormatMoney(supplierPayments)} supplier payments");
            AddReportCard("Inventory Valuation", $"{products.Count} product(s) · Cost {FormatMoney(stockCostValue)} · Retail {FormatMoney(stockRetailValue)} · {lowStock.Count} low-stock item(s)");
            AddReportCard("Stock Movement Report", $"{movements.Count} movement(s) · {movements.Count(x => x.Quantity > 0)} incoming · {movements.Count(x => x.Quantity < 0)} outgoing/adjusted");

            if (overdue.Count > 0)
            {
                var top = string.Join(", ", overdue.OrderBy(x => x.DueDate).Take(3).Select(x => $"{x.InvoiceNumber} ({FormatMoney(Math.Max(0, InvoiceTotal(x) - x.PaidAmount))})"));
                AddReportCard("Overdue Receivables", top + (overdue.Count > 3 ? $" + {overdue.Count - 3} more" : string.Empty));
            }

            if (lowStock.Count > 0)
            {
                var top = string.Join(", ", lowStock.Take(5).Select(x => $"{x.Name} ({x.Stock}/{x.MinimumStock})"));
                AddReportCard("Low Stock", top + (lowStock.Count > 5 ? $" + {lowStock.Count - 5} more" : string.Empty));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Reports load failed: {ex}");
            ResetValues();
        }
    }

    private static decimal InvoiceTotal(AveroNova.Domain.Entities.Invoice invoice)
    {
        var active = invoice.Items.Where(i => !i.IsDeleted).ToList();
        var subtotal = active.Sum(i => i.UnitPrice * i.Quantity * (1m - i.DiscountPct / 100m));
        var lineTax = active.Sum(i => i.UnitPrice * i.Quantity * (1m - i.DiscountPct / 100m) * i.TaxPct / 100m);
        return subtotal + lineTax + subtotal * invoice.TaxPct / 100m - subtotal * invoice.DiscountPct / 100m;
    }

    private static decimal PurchaseTotal(AveroNova.Domain.Entities.Purchase purchase)
        => purchase.Items.Where(i => !i.IsDeleted).Sum(i => i.UnitPrice * i.Quantity * (1m + i.TaxPct / 100m));

    private void AddReportCard(string title, string description)
    {
        ReportsList.Children.Add(new Border
        {
            Style = (Style)Resources["AppCard"],
            Padding = new Thickness(16),
            Content = new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new Label { Text = title, FontSize = 14, FontAttributes = FontAttributes.Bold },
                    new Label { Text = description, FontSize = 12, TextColor = Color.FromArgb("#64748B"), LineBreakMode = LineBreakMode.WordWrap }
                }
            }
        });
    }

    private void ResetValues()
    {
        RevenueValue.Text = "₹0.00";
        PurchaseValue.Text = "₹0.00";
        ProfitValue.Text = "₹0.00";
        OutstandingValue.Text = "₹0.00";
        ReportsList.Children.Clear();
        AddReportCard("Sales Report", "No posted local sales data available.");
        AddReportCard("Purchase Report", "No local purchase data available.");
        AddReportCard("Payment Report", "No local payment data available.");
        AddReportCard("Inventory Valuation", "No local inventory data available.");
    }

    private static string FormatMoney(decimal amount) => $"₹{amount:N2}";
}
