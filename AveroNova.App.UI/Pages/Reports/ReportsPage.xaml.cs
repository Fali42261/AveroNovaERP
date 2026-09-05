using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Reports;

public partial class ReportsPage : ContentPage
{
    private readonly IReportingService _reporting;
    private readonly ICompanyService _company;

    public ReportsPage(IReportingService reporting, ICompanyService company)
    {
        InitializeComponent();
        _reporting = reporting;
        _company = company;
        FromDate.Date = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        ToDate.Date = DateTime.Today;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async void OnRefreshing(object s, EventArgs e)
    {
        await LoadAsync();
        Refresher.IsRefreshing = false;
    }

    private async void OnApplyClicked(object? sender, EventArgs e) => await LoadAsync();

    private async Task LoadAsync()
    {
        ErrorBanner.IsVisible = false;
        var companyId = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        var from = FromDate.Date ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var to = ToDate.Date ?? DateTime.Today;
        var (summary, error) = await _reporting.GetSummaryAsync(companyId, new ReportPeriod(from, to));
        if (summary is null)
        {
            LblError.Text = error ?? "Report could not be loaded.";
            ErrorBanner.IsVisible = true;
            return;
        }

        LblRevenue.Text = Money(summary.NetRevenue);
        LblExpenses.Text = Money(summary.OperatingExpenses);
        LblProfit.Text = Money(summary.NetProfit);
        LblOutstanding.Text = Money(summary.OutstandingReceivables);

        ReportsList.Children.Clear();
        AddRow("Sales", $"{summary.InvoiceCount} invoices", summary.GrossSales);
        AddRow("Sales returns", "Completed refunds", -summary.SalesReturns);
        AddRow("Purchases", $"{summary.PurchaseCount} orders", summary.GrossPurchases);
        AddRow("Purchase returns", "Completed refunds", -summary.PurchaseReturns);
        AddRow("Operating expenses", "Approved / paid", summary.OperatingExpenses);
        AddRow("Payments received", "Completed", summary.PaymentsReceived);
        AddRow("Payments paid", "Completed supplier payments", summary.PaymentsPaid);
        AddRow("Outstanding payables", "Pending supplier payment", summary.OutstandingPayables);
    }

    private void AddRow(string title, string detail, decimal amount)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)),
            Padding = new Thickness(16, 12),
            ColumnSpacing = 12
        };
        var text = new VerticalStackLayout { Spacing = 2 };
        text.Children.Add(new Label { Text = title, FontSize = 13, FontAttributes = FontAttributes.Bold });
        text.Children.Add(new Label { Text = detail, FontSize = 11, TextColor = Color.FromArgb("#64748B") });
        row.Add(text, 0, 0);
        row.Add(new Label { Text = Money(amount), FontSize = 13, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }, 1, 0);
        ReportsList.Children.Add(new Border { Style = (Style)Microsoft.Maui.Controls.Application.Current!.Resources["AppCard"], Content = row });
    }

    private static string Money(decimal amount) => "$" + amount.ToString("N2");
}
