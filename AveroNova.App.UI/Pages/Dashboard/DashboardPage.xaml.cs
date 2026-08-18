using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Dashboard;

public partial class DashboardPage : ContentPage
{
    private readonly IBillingService _billing;
    private readonly IProductService _product;
    private readonly ICustomerService _customer;
    private readonly IPaymentService _payment;
    private readonly IPurchaseService _purchase;
    private readonly ICompanyService _company;
    private readonly IAuthenticationService _auth;
    private readonly ISubscriptionService _subscription;

    public DashboardPage(
        IBillingService billing,
        IProductService product,
        ICustomerService customer,
        IPaymentService payment,
        IPurchaseService purchase,
        ICompanyService company,
        IAuthenticationService auth,
        ISubscriptionService subscription)
    {
        InitializeComponent();
        _billing = billing;
        _product = product;
        _customer = customer;
        _payment = payment;
        _purchase = purchase;
        _company = company;
        _auth = auth;
        _subscription = subscription;
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
        var user = _auth.CurrentUser;
        var hour = DateTime.Now.Hour;
        var greeting = hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";
        LblWelcome.Text = user == null
            ? greeting
            : $"{greeting}, {user.Name}";

        var company = _company.CurrentCompany;
        var cid = company?.LocalId ?? Guid.Empty;
        var subscription = cid == Guid.Empty
            ? null
            : await _subscription.GetCurrentAsync(cid);

        BuildCompanySection(company, user, subscription);

        var invoices = cid == Guid.Empty ? [] : await _billing.GetAllAsync(cid);
        var products = cid == Guid.Empty ? [] : await _product.GetAllAsync(cid);
        var customers = cid == Guid.Empty ? [] : await _customer.GetAllAsync(cid);
        var payments = cid == Guid.Empty ? [] : await _payment.GetAllAsync(cid);
        var purchases = cid == Guid.Empty ? [] : await _purchase.GetAllAsync(cid);

        LblTotalSales.Text = "$" + invoices.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.GrandTotal).ToString("N0");
        LblTotalPurchases.Text = "$" + purchases.Sum(p => p.GrandTotal).ToString("N0");
        LblOutstanding.Text = "$" + invoices.Where(i => i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled).Sum(i => i.DueAmount).ToString("N0");
        LblCustomers.Text = customers.Count.ToString();
        LblProducts.Text = products.Count.ToString();
        LblPayments.Text = "$" + payments.Sum(p => p.Amount).ToString("N0");

        LblSalesChange.Text = invoices.Count == 0 ? "No data" : $"{invoices.Count} invoices";
        LblPurchasesMeta.Text = purchases.Count == 0 ? "No data" : $"{purchases.Count} orders";
        var outstandingCount = invoices.Count(i => i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled);
        LblOutstandingMeta.Text = outstandingCount == 0 ? "No data" : $"{outstandingCount} invoices";
        LblCustomersMeta.Text = customers.Count == 0 ? "No data" : $"{customers.Count} total";
        var lowStock = products.Count(p => p.IsLowStock);
        LblProductsMeta.Text = lowStock == 0 ? "No data" : $"{lowStock} low stock";
        LblPaymentsMeta.Text = payments.Count == 0 ? "No data" : $"{payments.Count} received";
        LblLowStockCount.Text = $"{lowStock} items";

        InvoiceList.Children.Clear();
        bool first = true;
        foreach (var inv in invoices.OrderByDescending(i => i.InvoiceDate).Take(4))
        {
            if (!first)
                InvoiceList.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#F1F5F9"), HorizontalOptions = LayoutOptions.Fill });
            InvoiceList.Children.Add(BuildInvoiceRow(inv));
            first = false;
        }

        if (invoices.Count == 0)
            InvoiceList.Children.Add(new Label { Text = "  No data", FontSize = 13, TextColor = Color.FromArgb("#64748B"), Padding = new Thickness(18, 14) });

        LowStockList.Children.Clear();
        first = true;
        foreach (var p in products.Where(p => p.IsLowStock).Take(4))
        {
            if (!first)
                LowStockList.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#F1F5F9"), HorizontalOptions = LayoutOptions.Fill });
            LowStockList.Children.Add(BuildLowStockRow(p));
            first = false;
        }

        if (!products.Any(p => p.IsLowStock))
            LowStockList.Children.Add(new Label { Text = "  No low stock items.", FontSize = 13, TextColor = Color.FromArgb("#64748B"), Padding = new Thickness(18, 14) });
    }

    private void BuildCompanySection(CompanyModel? company, UserModel? user, SubscriptionModel? subscription)
    {
        CompanyDetailsHost.Children.Clear();

        if (IsUnderReview(subscription))
            CompanyDetailsHost.Children.Add(BuildUnderReviewCard(subscription));

        CompanyDetailsHost.Children.Add(BuildCompanyDetailsCard(company, user, subscription));
    }

    private static bool IsUnderReview(SubscriptionModel? subscription)
    {
        if (subscription == null)
            return false;

        var plan = (subscription.PlanId ?? subscription.PlanName ?? string.Empty).Trim().ToLowerInvariant();
        if (plan is "business" or "enterprise")
            return true;

        return subscription.Status is SubscriptionStatus.PendingRenewal or SubscriptionStatus.Cancelled;
    }

    private static View BuildUnderReviewCard(SubscriptionModel? subscription)
    {
        var card = new Border { Style = ResolveStyle("AppCard"), Padding = 18 };
        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add(new Label
        {
            Text = "Under Review",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        });
        stack.Children.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(subscription?.PlanName)
                ? "Your account is under review."
                : $"{subscription.PlanName} is under review. You can continue using AveroNova with locally saved company data.",
            FontSize = 13,
            TextColor = Color.FromArgb("#64748B"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        card.Content = stack;
        return card;
    }

    private static View BuildCompanyDetailsCard(CompanyModel? company, UserModel? user, SubscriptionModel? subscription)
    {
        var card = new Border { Style = ResolveStyle("AppCard"), Padding = 18 };
        var stack = new VerticalStackLayout { Spacing = 10 };
        stack.Children.Add(new Label
        {
            Text = "Company Details",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold
        });

        if (company == null)
        {
            stack.Children.Add(new Label
            {
                Text = "No company data",
                FontSize = 13,
                TextColor = Color.FromArgb("#64748B")
            });
            card.Content = stack;
            return card;
        }

        AddDetail(stack, "Company Name", company.Name);
        AddDetail(stack, "Company Code", company.CompanyCode);
        AddDetail(stack, "Owner", string.IsNullOrWhiteSpace(company.OwnerName) ? user?.Name : company.OwnerName);
        AddDetail(stack, "Mobile", company.Phone);
        AddDetail(stack, "Email", company.Email);
        AddDetail(stack, "Address", FormatAddress(company));
        AddDetail(stack, "GST Number", company.TaxNumber);
        AddDetail(stack, "PAN Number", company.RegistrationNo);
        AddDetail(stack, "Subscription", FormatSubscription(subscription));

        card.Content = stack;
        return card;
    }

    private static void AddDetail(VerticalStackLayout stack, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(new GridLength(120)),
                new ColumnDefinition(GridLength.Star)),
            ColumnSpacing = 8
        };
        row.Add(new Label
        {
            Text = label,
            FontSize = 12,
            TextColor = Color.FromArgb("#64748B"),
            VerticalOptions = LayoutOptions.Start
        }, 0, 0);
        row.Add(new Label
        {
            Text = value,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        }, 1, 0);
        stack.Children.Add(row);
    }

    private static string FormatAddress(CompanyModel company)
    {
        var parts = new[]
        {
            company.Address,
            company.City,
            company.State,
            company.PinCode,
            company.Country
        }.Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(", ", parts);
    }

    private static string FormatSubscription(SubscriptionModel? subscription)
    {
        if (subscription == null)
            return "No data";

        if (subscription.IsTrial)
            return $"{subscription.PlanName} — 15-day free trial (ends {subscription.ExpiryDate:dd MMM yyyy})";

        return $"{subscription.PlanName} — {subscription.StatusLabel}";
    }

    private static Style? ResolveStyle(string key)
        => Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is Style style
            ? style
            : null;

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
        var statusBg = inv.Status switch
        {
            InvoiceStatus.Paid => "#ECFDF5",
            InvoiceStatus.Overdue => "#FEF2F2",
            InvoiceStatus.Sent => "#EFF6FF",
            _ => "#F9FAFB"
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
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            Padding = new Thickness(8, 2)
        };
        badge.Content = new Label { Text = inv.StatusLabel, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(statusColor) };
        right.Children.Add(badge);

        grid.Add(left, 0, 0);
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

        grid.Add(left, 0, 0);
        grid.Add(right, 1, 0);
        return grid;
    }
}
