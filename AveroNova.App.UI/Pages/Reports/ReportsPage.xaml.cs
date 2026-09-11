using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Reports;

public partial class ReportsPage : ContentPage, IHostedPage
{
    private readonly IReportingService _reporting;
    private readonly ICompanyService _company;
    private readonly ISettingsService _settings;
    private int _loadVersion;
    private string _currencySymbol = "₹";

    public ReportsPage(IReportingService reporting, ICompanyService company, ISettingsService settings)
    {
        InitializeComponent();
        _reporting = reporting;
        _company = company;
        _settings = settings;
        FromDate.Date = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        ToDate.Date = DateTime.Today;
    }

    protected override async void OnAppearing() { base.OnAppearing(); await LoadForHostAsync(); }
    public async Task LoadForHostAsync() => await LoadAsync();
    private async void OnRefreshing(object? s, EventArgs e) { try { await LoadAsync(); } finally { Refresher.IsRefreshing = false; } }
    private async void OnApplyClicked(object? sender, EventArgs e) => await LoadAsync();

    private async Task LoadAsync()
    {
        var version = Interlocked.Increment(ref _loadVersion);
        ErrorBanner.IsVisible = false;
        var companyId = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        var from = FromDate.Date ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var to = ToDate.Date ?? DateTime.Today;
        ClearReport();
        if (companyId == Guid.Empty) { ShowError("No company is selected."); return; }

        try
        {
            var settings = await _settings.GetAsync();
            _currencySymbol = string.IsNullOrWhiteSpace(settings.CurrencySymbol) ? (settings.Currency == "INR" ? "₹" : "$") : settings.CurrencySymbol;
            var (summary,error) = await _reporting.GetSummaryAsync(companyId,new ReportPeriod(from,to));
            if (version != _loadVersion || _company.CurrentCompany?.LocalId != companyId) return;
            if (summary is null) { ShowError(error ?? "Report could not be loaded."); return; }

            LblRevenue.Text = Money(summary.NetRevenue);
            LblExpenses.Text = Money(summary.OperatingExpenses);
            LblProfit.Text = Money(summary.NetProfit);
            LblOutstanding.Text = Money(summary.OutstandingReceivables);

            AddRow("Sales", $"{summary.InvoiceCount} invoices", summary.NetRevenue);
            AddRow("Purchases", $"{summary.PurchaseCount} orders", summary.NetPurchases);
            AddRow("Expenses", "Business expenses", summary.OperatingExpenses);
            AddRow("Payments received", "Customer payments", summary.PaymentsReceived);
            AddRow("Pending collection", "Customer outstanding", summary.OutstandingReceivables);
            AddRow("Pending supplier payment", "Supplier outstanding", summary.OutstandingPayables);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Reports] Load failed: {ex}");
            if (version == _loadVersion) ShowError("Report could not be loaded. Pull down to retry.");
        }
    }

    private void ClearReport()
    {
        LblRevenue.Text = Money(0); LblExpenses.Text = Money(0); LblProfit.Text = Money(0); LblOutstanding.Text = Money(0); ReportsList.Children.Clear();
    }

    private void ShowError(string message) { LblError.Text = message; ErrorBanner.IsVisible = true; }

    private void AddRow(string title,string detail,decimal amount)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)), Padding = new Thickness(16,12), ColumnSpacing = 12 };
        var text = new VerticalStackLayout { Spacing = 2 };
        text.Children.Add(new Label { Text = title, FontSize = 13, FontAttributes = FontAttributes.Bold });
        text.Children.Add(new Label { Text = detail, FontSize = 11, TextColor = Color.FromArgb("#64748B") });
        row.Add(text,0,0);
        row.Add(new Label { Text = Money(amount), FontSize = 13, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center },1,0);
        ReportsList.Children.Add(new Border { Stroke = Color.FromArgb("#E2E8F0"), StrokeThickness = 1, Padding = 0, Content = row });
    }

    private string Money(decimal amount) => $"{_currencySymbol}{amount:N2}";
}
