using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Enums;

namespace AveroNova.App.UI.Pages.Dashboard;

public partial class DashboardPage : ContentPage, IHostedPage
{
    private readonly IBillingService _billing;
    private readonly IProductService _product;
    private readonly ICompanyService _company;
    private readonly ILicenseService _licenses;
    private readonly IReportingService _reporting;
    private int _loadVersion;

    public DashboardPage(
        IBillingService billing,
        IProductService product,
        ICompanyService company,
        ILicenseService licenses,
        IReportingService reporting)
    {
        InitializeComponent();
        _billing = billing;
        _product = product;
        _company = company;
        _licenses = licenses;
        _reporting = reporting;
        Loaded += async (_, _) => await LoadForHostAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }

    public async Task LoadForHostAsync()
    {
        LblDate.Text = DateTime.Today.ToString("dddd, dd MMMM yyyy");
        LblFiscalYear.Text = $"FY {DateTime.Today:yyyy}";
        LblWelcome.Text = string.IsNullOrWhiteSpace(_company.CurrentCompany?.Name)
            ? "Dashboard"
            : _company.CurrentCompany!.Name;
        await LoadDataAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            await LoadForHostAsync();
        }
        finally
        {
            Refresher.IsRefreshing = false;
        }
    }

    private async Task LoadDataAsync()
    {
        var loadVersion = Interlocked.Increment(ref _loadVersion);
        var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        ClearBusinessData();

        if (cid == Guid.Empty)
        {
            TrialBanner.IsVisible = true;
            LblTrialTitle.Text = "No company selected";
            LblTrialDetail.Text = "Select or create a company to load dashboard data.";
            return;
        }

        try
        {
            await LoadLicenseBannerAsync();
            var invoices = await _billing.GetAllAsync(cid);
            var products = await _product.GetAllAsync(cid);
            var (summary, _) = await _reporting.GetSummaryAsync(cid, ReportPeriod.CurrentMonth(DateTime.Today));

            if (!IsCurrentLoad(loadVersion, cid) || summary is null)
                return;

            LblTotalSales.Text = Money(summary.NetRevenue);
            LblTotalPurchases.Text = Money(summary.NetPurchases);
            LblOutstanding.Text = Money(summary.OutstandingReceivables);
            LblCustomers.Text = summary.CustomerCount.ToString();
            LblProducts.Text = summary.ProductCount.ToString();
            LblPayments.Text = Money(summary.PaymentsReceived);
            LblSalesCount.Text = $"{summary.InvoiceCount} invoices";
            LblPurchaseCount.Text = $"{summary.PurchaseCount} orders";
            LblOverdueCount.Text = $"{summary.OverdueInvoiceCount} overdue";
            LblActiveCustomers.Text = $"{summary.ActiveCustomerCount} active";
            LblLowStockCount.Text = $"{summary.LowStockCount} low stock";
            LblLowStockHeaderCount.Text = $"{summary.LowStockCount} items";

            RenderInvoices(invoices);
            RenderLowStock(products);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Load failed: {ex}");
            if (IsCurrentLoad(loadVersion, cid))
            {
                ClearBusinessData();
                TrialBanner.IsVisible = true;
                LblTrialTitle.Text = "Dashboard unavailable";
                LblTrialDetail.Text = "Local data could not be loaded right now. Pull to refresh; the app will stay open.";
            }
        }
    }

    private void RenderInvoices(List<InvoiceModel> invoices)
    {
        InvoiceList.Children.Clear();
        var recent = invoices.OrderByDescending(i => i.InvoiceDate).Take(4).ToList();
        if (recent.Count == 0)
        {
            InvoiceList.Children.Add(EmptyLabel("No invoices yet."));
            return;
        }

        var first = true;
        foreach (var inv in recent)
        {
            if (!first) InvoiceList.Children.Add(Divider());
            InvoiceList.Children.Add(BuildInvoiceRow(inv));
            first = false;
        }
    }

    private void RenderLowStock(List<ProductModel> products)
    {
        LowStockList.Children.Clear();
        var lowStock = products.Where(p => p.IsLowStock).Take(4).ToList();
        if (lowStock.Count == 0)
        {
            LowStockList.Children.Add(EmptyLabel("No low stock items."));
            return;
        }

        var first = true;
        foreach (var p in lowStock)
        {
            if (!first) LowStockList.Children.Add(Divider());
            LowStockList.Children.Add(BuildLowStockRow(p));
            first = false;
        }
    }

    private bool IsCurrentLoad(int loadVersion, Guid companyId) =>
        loadVersion == _loadVersion && _company.CurrentCompany?.LocalId == companyId;

    private void ClearBusinessData()
    {
        LblTotalSales.Text = Money(0);
        LblTotalPurchases.Text = Money(0);
        LblOutstanding.Text = Money(0);
        LblCustomers.Text = "0";
        LblProducts.Text = "0";
        LblPayments.Text = Money(0);
        LblSalesCount.Text = "0 invoices";
        LblPurchaseCount.Text = "0 orders";
        LblOverdueCount.Text = "0 overdue";
        LblActiveCustomers.Text = "0 active";
        LblLowStockCount.Text = "0 low stock";
        LblLowStockHeaderCount.Text = "0 items";
        InvoiceList.Children.Clear();
        LowStockList.Children.Clear();
    }

    private static string Money(decimal amount) => "₹" + amount.ToString("N0");

    private async Task LoadLicenseBannerAsync()
    {
        var state = await _licenses.GetAccessStateAsync();
        TrialBanner.IsVisible = true;

        if (state.NeedsFirstActivation)
        {
            LblTrialTitle.Text = "Free Plan";
            LblTrialDetail.Text = "Free access is active for testing. Paid plans remain preview-only.";
            return;
        }

        if (state.Status == LicenseStatus.Expired)
        {
            LblTrialTitle.Text = "Access status";
            LblTrialDetail.Text = "Some licensed features are unavailable. Free functionality remains available.";
            return;
        }

        if (state.IsTrial)
        {
            LblTrialTitle.Text = "Free Plan";
            LblTrialDetail.Text = "Free plan is enabled while customer testing is in progress.";
            return;
        }

        LblTrialTitle.Text = string.IsNullOrWhiteSpace(state.Plan) ? "Free Plan" : state.Plan;
        LblTrialDetail.Text = "Current plan status is active on this device.";
    }

    private static View BuildInvoiceRow(InvoiceModel inv)
    {
        var statusColor = inv.Status switch
        {
            InvoiceStatus.Paid => "#059669",
            InvoiceStatus.Overdue => "#DC2626",
            InvoiceStatus.Sent => "#2563EB",
            InvoiceStatus.Draft => "#6B7280",
            InvoiceStatus.Cancelled => "#9CA3AF",
            _ => "#D97706"
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            Padding = new Thickness(16, 12),
            ColumnSpacing = 12
        };

        var left = new VerticalStackLayout { Spacing = 3 };
        left.Children.Add(new Label { Text = inv.InvoiceNumber, FontSize = 13, FontAttributes = FontAttributes.Bold });
        left.Children.Add(new Label { Text = inv.CustomerName, FontSize = 12, TextColor = Color.FromArgb("#64748B") });

        var right = new VerticalStackLayout { Spacing = 3, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label
        {
            Text = Money(inv.GrandTotal),
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.End
        });
        right.Children.Add(new Label
        {
            Text = inv.StatusLabel,
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb(statusColor),
            HorizontalOptions = LayoutOptions.End
        });

        grid.Add(left, 0, 0);
        grid.Add(right, 1, 0);
        return grid;
    }

    private static View BuildLowStockRow(ProductModel p)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            Padding = new Thickness(16, 12),
            ColumnSpacing = 12
        };

        var left = new VerticalStackLayout { Spacing = 3 };
        left.Children.Add(new Label { Text = p.Name, FontSize = 13, FontAttributes = FontAttributes.Bold });
        left.Children.Add(new Label { Text = $"SKU: {p.SKU}", FontSize = 12, TextColor = Color.FromArgb("#64748B") });

        var right = new VerticalStackLayout { Spacing = 3, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = $"{p.Stock} left", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#DC2626"), HorizontalOptions = LayoutOptions.End });
        right.Children.Add(new Label { Text = $"Min: {p.MinimumStock}", FontSize = 11, TextColor = Color.FromArgb("#94A3B8"), HorizontalOptions = LayoutOptions.End });

        grid.Add(left, 0, 0);
        grid.Add(right, 1, 0);
        return grid;
    }

    private static View Divider() => new BoxView
    {
        HeightRequest = 1,
        BackgroundColor = Color.FromArgb("#F1F5F9"),
        HorizontalOptions = LayoutOptions.Fill
    };

    private static View EmptyLabel(string text) => new Label
    {
        Text = text,
        FontSize = 13,
        TextColor = Color.FromArgb("#64748B"),
        Padding = new Thickness(16, 14)
    };
}
