using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Enums;

namespace AveroNova.App.UI.Pages.Dashboard;

public partial class DashboardPage : ContentPage
{
    private readonly IBillingService   _billing;
    private readonly IProductService   _product;
    private readonly ICustomerService  _customer;
    private readonly IPaymentService   _payment;
    private readonly ICompanyService   _company;
    private readonly ILicenseService   _licenses;

    public DashboardPage(
        IBillingService  billing,
        IProductService  product,
        ICustomerService customer,
        IPaymentService  payment,
        ICompanyService  company,
        ILicenseService  licenses)
    {
        InitializeComponent();
        _billing  = billing;
        _product  = product;
        _customer = customer;
        _payment  = payment;
        _company  = company;
        _licenses = licenses;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LblDate.Text = DateTime.Today.ToString("dddd, dd MMMM yyyy");
        await LoadDataAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadDataAsync();
        Refresher.IsRefreshing = false;
    }

    private async Task LoadDataAsync()
    {
        await LoadLicenseBannerAsync();

        var cid       = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        var invoices  = await _billing.GetAllAsync(cid);
        var products  = await _product.GetAllAsync(cid);
        var customers = await _customer.GetAllAsync(cid);
        var payments  = await _payment.GetAllAsync(cid);

        // KPI
        LblTotalSales.Text    = "$" + invoices.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.GrandTotal).ToString("N0");
        LblOutstanding.Text   = "$" + invoices.Where(i => i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled).Sum(i => i.DueAmount).ToString("N0");
        LblCustomers.Text     = customers.Count.ToString();
        LblProducts.Text      = products.Count.ToString();
        LblPayments.Text      = "$" + payments.Sum(p => p.Amount).ToString("N0");

        // Recent invoices (last 4)
        InvoiceList.Children.Clear();
        bool first = true;
        foreach (var inv in invoices.OrderByDescending(i => i.InvoiceDate).Take(4))
        {
            if (!first) InvoiceList.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#F1F5F9"), HorizontalOptions = LayoutOptions.Fill });
            InvoiceList.Children.Add(BuildInvoiceRow(inv));
            first = false;
        }

        // Low stock
        LowStockList.Children.Clear();
        first = true;
        foreach (var p in products.Where(p => p.IsLowStock).Take(4))
        {
            if (!first) LowStockList.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#F1F5F9"), HorizontalOptions = LayoutOptions.Fill });
            LowStockList.Children.Add(BuildLowStockRow(p));
            first = false;
        }

        if (!products.Any(p => p.IsLowStock))
            LowStockList.Children.Add(new Label { Text = "  No low stock items.", FontSize = 13, TextColor = Color.FromArgb("#64748B"), Padding = new Thickness(18, 14) });
    }

    private async Task LoadLicenseBannerAsync()
    {
        var state = await _licenses.GetAccessStateAsync();
        if (state.NeedsFirstActivation)
        {
            TrialBanner.IsVisible = false;
            return;
        }

        TrialBanner.IsVisible = true;
        if (state.Status == LicenseStatus.Expired)
        {
            LblTrialTitle.Text = "License expired";
            LblTrialDetail.Text = "Restricted features are unavailable until the license is renewed.";
            return;
        }

        if (state.IsTrial)
        {
            LblTrialTitle.Text = "Starter · Trial";
            var end = state.TrialEndDateUtc?.ToLocalTime().ToString("dd-MMM-yyyy");
            LblTrialDetail.Text = $"{state.RemainingTrialDays} day{(state.RemainingTrialDays == 1 ? "" : "s")} remaining · Ends {end}";
            return;
        }

        LblTrialTitle.Text = $"{state.Plan} · {state.Status}";
        LblTrialDetail.Text = "License is active on this device.";
    }

    private static View BuildInvoiceRow(InvoiceModel inv)
    {
        var statusColor = inv.Status switch
        {
            InvoiceStatus.Paid        => "#059669",
            InvoiceStatus.Overdue     => "#DC2626",
            InvoiceStatus.Sent        => "#2563EB",
            InvoiceStatus.Draft       => "#6B7280",
            InvoiceStatus.Cancelled   => "#9CA3AF",
            _                         => "#D97706"
        };
        var statusBg = inv.Status switch
        {
            InvoiceStatus.Paid    => "#ECFDF5",
            InvoiceStatus.Overdue => "#FEF2F2",
            InvoiceStatus.Sent    => "#EFF6FF",
            _                     => "#F9FAFB"
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), Padding = new Thickness(18, 12), ColumnSpacing = 12 };

        var left = new VerticalStackLayout { Spacing = 3 };
        left.Children.Add(new Label { Text = inv.InvoiceNumber, FontSize = 13, FontAttributes = FontAttributes.Bold });
        left.Children.Add(new Label { Text = inv.CustomerName, FontSize = 12, TextColor = Color.FromArgb("#64748B") });

        var right = new VerticalStackLayout { Spacing = 3, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = "$" + inv.GrandTotal.ToString("N2"), FontSize = 13, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.End });

        var badge = new Border
        {
            BackgroundColor = Color.FromArgb(statusBg),
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(999) },
            Padding = new Thickness(8, 2)
        };
        badge.Content = new Label { Text = inv.StatusLabel, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(statusColor) };
        right.Children.Add(badge);

        grid.Add(left,  0, 0);
        grid.Add(right, 1, 0);
        return grid;
    }

    private static View BuildLowStockRow(ProductModel p)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), Padding = new Thickness(18, 12), ColumnSpacing = 12 };

        var left = new VerticalStackLayout { Spacing = 3 };
        left.Children.Add(new Label { Text = p.Name, FontSize = 13, FontAttributes = FontAttributes.Bold });
        left.Children.Add(new Label { Text = $"SKU: {p.SKU}", FontSize = 12, TextColor = Color.FromArgb("#64748B") });

        var right = new VerticalStackLayout { Spacing = 3, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = $"{p.Stock} left", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#DC2626"), HorizontalOptions = LayoutOptions.End });
        right.Children.Add(new Label { Text = $"Min: {p.MinimumStock}", FontSize = 11, TextColor = Color.FromArgb("#9CA3AF"), HorizontalOptions = LayoutOptions.End });

        grid.Add(left,  0, 0);
        grid.Add(right, 1, 0);
        return grid;
    }
}
