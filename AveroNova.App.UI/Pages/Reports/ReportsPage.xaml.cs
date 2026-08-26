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
        InitializeComponent(); _dbFactory = dbFactory; _company = company;
        Loaded += async (_, _) => await LoadAsync();
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
            var invoices = await db.Invoices.AsNoTracking().Include(x => x.Items).Where(x => x.CompanyId == companyId && !x.IsDeleted).ToListAsync();
            var purchases = await db.Purchases.AsNoTracking().Include(x => x.Items).Where(x => x.CompanyId == companyId && !x.IsDeleted).ToListAsync();
            var revenue = invoices.Sum(x => x.Items.Where(i => !i.IsDeleted).Sum(i => i.UnitPrice * i.Quantity * (1m - i.DiscountPct / 100m) * (1m + i.TaxPct / 100m)));
            var purchaseTotal = purchases.Sum(x => x.Items.Where(i => !i.IsDeleted).Sum(i => i.UnitPrice * i.Quantity * (1m + i.TaxPct / 100m)));
            var outstanding = invoices.Sum(x => Math.Max(0m, RevenueFor(x) - x.PaidAmount));
            RevenueValue.Text = FormatMoney(revenue); PurchaseValue.Text = FormatMoney(purchaseTotal); ProfitValue.Text = FormatMoney(revenue - purchaseTotal); OutstandingValue.Text = FormatMoney(outstanding);
            ReportsList.Children.Clear();
            AddReportCard("Sales Report", $"{invoices.Count} invoice(s) · {FormatMoney(revenue)} revenue");
            AddReportCard("Purchase Report", $"{purchases.Count} purchase order(s) · {FormatMoney(purchaseTotal)} purchases");
            AddReportCard("Receivables Report", $"{FormatMoney(outstanding)} currently outstanding");
            AddReportCard("Inventory / Stock Movement", "Use Inventory → Stock History for item-level movement.");
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AveroNova] Reports load failed: {ex}"); ResetValues(); }
    }

    private static decimal RevenueFor(AveroNova.Domain.Entities.Invoice invoice)
        => invoice.Items.Where(i => !i.IsDeleted).Sum(i => i.UnitPrice * i.Quantity * (1m - i.DiscountPct / 100m) * (1m + i.TaxPct / 100m));

    private void AddReportCard(string title, string description)
    {
        ReportsList.Children.Add(new Border
        {
            Style = (Style)Resources["AppCard"], Padding = new Thickness(16),
            Content = new VerticalStackLayout { Spacing = 4, Children = { new Label { Text = title, FontSize = 14, FontAttributes = FontAttributes.Bold }, new Label { Text = description, FontSize = 12, TextColor = Color.FromArgb("#64748B") } } }
        });
    }

    private void ResetValues()
    {
        RevenueValue.Text = "₹0.00"; PurchaseValue.Text = "₹0.00"; ProfitValue.Text = "₹0.00"; OutstandingValue.Text = "₹0.00"; ReportsList.Children.Clear();
        AddReportCard("Sales Report", "No local sales data available."); AddReportCard("Purchase Report", "No local purchase data available."); AddReportCard("Receivables Report", "No outstanding customer balance.");
    }

    private static string FormatMoney(decimal amount) => $"₹{amount:N2}";
}
