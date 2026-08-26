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
                .Where(x => x.CompanyId == companyId && !x.IsDeleted && x.Status != (int)InvoiceStatus.Cancelled)
                .ToListAsync();
            var purchases = await db.Purchases.AsNoTracking().Include(x => x.Items)
                .Where(x => x.CompanyId == companyId && !x.IsDeleted && x.Status != (int)PurchaseStatus.Cancelled)
                .ToListAsync();
            var payments = await db.Payments.AsNoTracking()
                .Where(x => x.CompanyId == companyId && !x.IsDeleted && x.Status == (int)PaymentStatus.Completed)
                .ToListAsync();
            var products = await db.Products.AsNoTracking()
                .Where(x => x.CompanyId == companyId && !x.IsDeleted)
                .ToListAsync();
            var movements = await db.StockMovements.AsNoTracking()
                .Where(x => x.CompanyId == companyId && !x.IsDeleted)
                .CountAsync();

            var revenue = invoices.Sum(InvoiceTotal);
            var purchaseTotal = purchases.Sum(PurchaseTotal);
            var receivable = invoices.Sum(x => Math.Max(0m, InvoiceTotal(x) - x.PaidAmount));
            var payable = purchases.Sum(x => Math.Max(0m, PurchaseTotal(x) - x.PaidAmount));
            var customerCollections = payments.Where(x => !x.IsSupplier).Sum(x => x.Amount);
            var supplierPayments = payments.Where(x => x.IsSupplier).Sum(x => x.Amount);
            var stockCostValue = products.Sum(x => x.PurchasePrice * x.Stock);
            var stockRetailValue = products.Sum(x => x.SellingPrice * x.Stock);
            var lowStockCount = products.Count(x => x.Stock <= x.MinimumStock);

            RevenueValue.Text = FormatMoney(revenue);
            PurchaseValue.Text = FormatMoney(purchaseTotal);
            ProfitValue.Text = FormatMoney(revenue - purchaseTotal);
            OutstandingValue.Text = FormatMoney(receivable);

            ReportsList.Children.Clear();
            AddReportCard("Sales Report", $"{invoices.Count} invoice(s) · {FormatMoney(revenue)} total sales · {FormatMoney(receivable)} receivable");
            AddReportCard("Purchase Report", $"{purchases.Count} purchase(s) · {FormatMoney(purchaseTotal)} total purchases · {FormatMoney(payable)} payable");
            AddReportCard("Payment Report", $"{payments.Count} completed payment(s) · {FormatMoney(customerCollections)} received · {FormatMoney(supplierPayments)} paid to suppliers");
            AddReportCard("Inventory Valuation", $"{products.Count} product(s) · Cost {FormatMoney(stockCostValue)} · Retail {FormatMoney(stockRetailValue)} · {lowStockCount} low-stock item(s)");
            AddReportCard("Stock Movement Report", $"{movements} movement record(s) available in Inventory → Stock History");
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
                Spacing = 4,
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
        AddReportCard("Sales Report", "No local sales data available.");
        AddReportCard("Purchase Report", "No local purchase data available.");
        AddReportCard("Payment Report", "No local payment data available.");
        AddReportCard("Inventory Valuation", "No local inventory data available.");
    }

    private static string FormatMoney(decimal amount) => $"₹{amount:N2}";
}
