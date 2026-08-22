using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Dashboard;

public partial class DashboardPage : ContentPage
{
    private readonly IDashboardService _dashboard;
    private readonly ICompanyService _company;
    private readonly IAuthenticationService _auth;
    private readonly ISubscriptionService _subscription;

    public DashboardPage(IDashboardService dashboard, ICompanyService company, IAuthenticationService auth, ISubscriptionService subscription)
    {
        InitializeComponent();
        _dashboard = dashboard; _company = company; _auth = auth; _subscription = subscription;
    }

    public Task ReloadAsync() => LoadDataAsync();
    protected override async void OnAppearing() { base.OnAppearing(); await LoadDataAsync(); }
    private async void OnRefreshing(object sender, EventArgs e) { try { await LoadDataAsync(); } finally { Refresher.IsRefreshing = false; } }

    private async void OnNewInvoiceClicked(object sender, EventArgs e) => await NavigateToAsync(AppRoutes.InvoiceNew);
    private async void OnNewCustomerClicked(object sender, EventArgs e) => await NavigateToAsync(AppRoutes.CustomerAdd);
    private async void OnNewPurchaseClicked(object sender, EventArgs e) => await NavigateToAsync(AppRoutes.PurchaseNew);
    private async void OnStockAdjustClicked(object sender, EventArgs e) => await NavigateToAsync(AppRoutes.StockAdjust);
    private void OnViewAllInvoicesClicked(object sender, EventArgs e) => MainContentNavigator.Request(MainContentNavigator.Billing);
    private void OnViewInventoryClicked(object sender, EventArgs e) => MainContentNavigator.Request(MainContentNavigator.Inventory);
    private static async Task NavigateToAsync(string route) { try { await Shell.Current.GoToAsync(route); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Dashboard navigation failed for {route}: {ex}"); } }

    private async Task OpenInvoiceAsync(Guid invoiceId)
    {
        if (invoiceId == Guid.Empty) return;
        try { await Shell.Current.GoToAsync($"{AppRoutes.InvoiceView}?id={invoiceId}"); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Invoice navigation failed: {ex}"); }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var snapshot = await _dashboard.GetSnapshotAsync();
            LblDate.Text = snapshot.CurrentDate; LblWelcome.Text = snapshot.WelcomeMessage;
            var company = _company.CurrentCompany; var user = _auth.CurrentUser;
            var companyId = company?.LocalId ?? user?.CompanyId ?? Guid.Empty;
            var subscription = companyId == Guid.Empty ? null : await _subscription.GetCurrentAsync(companyId);
            BuildCompanySection(company, user, subscription);
            if (subscription?.IsExpired == true) return;
            var symbol = ResolveCurrencySymbol(snapshot.CurrencySymbol, company?.CurrencySymbol);
            LblTotalSales.Text = FormatMoney(symbol, snapshot.MonthSales); LblTotalPurchases.Text = FormatMoney(symbol, snapshot.TodayCollection);
            LblOutstanding.Text = FormatMoney(symbol, snapshot.TodayOutstanding); LblCustomers.Text = snapshot.TotalCustomers.ToString(); LblProducts.Text = snapshot.TotalProducts.ToString(); LblPayments.Text = FormatMoney(symbol, snapshot.TodayCollection);
            LblSalesChange.Text = snapshot.TodayInvoiceCount == 0 ? "No data" : $"{snapshot.TodayInvoiceCount} today";
            LblPurchasesMeta.Text = snapshot.TodayPaymentCount == 0 ? "No data" : $"{snapshot.TodayPaymentCount} today";
            LblOutstandingMeta.Text = snapshot.PendingPaymentCount == 0 ? "No data" : $"{snapshot.PendingPaymentCount} invoices";
            LblCustomersMeta.Text = snapshot.TotalCustomers == 0 ? "No data" : $"{snapshot.TotalCustomers} total";
            LblProductsMeta.Text = snapshot.LowStockCount == 0 ? "No data" : $"{snapshot.LowStockCount} low stock";
            LblPaymentsMeta.Text = snapshot.TodayPaymentCount == 0 ? "No data" : $"{snapshot.TodayPaymentCount} received";

            InvoiceList.Children.Clear(); foreach (var transaction in snapshot.RecentTransactions) InvoiceList.Children.Add(BuildInvoiceRow(transaction));
            if (snapshot.RecentTransactions.Count == 0) InvoiceList.Children.Add(new Label { Text = "No recent invoices.", FontSize = 13, TextColor = Color.FromArgb("#64748B"), Padding = new Thickness(18, 14) });

            LowStockList.Children.Clear();
            if (snapshot.LowStockItems.Count == 0)
            {
                LowStockList.Children.Add(new Label { Text = "No low stock items.", FontSize = 13, TextColor = Color.FromArgb("#64748B"), Padding = new Thickness(18, 14) });
            }
            else
            {
                foreach (var item in snapshot.LowStockItems) LowStockList.Children.Add(BuildLowStockRow(item));
            }
        }
        catch (Exception ex)
        {
            InvoiceList.Children.Clear(); LowStockList.Children.Clear();
            LowStockList.Children.Add(new Label { Text = "Unable to load dashboard data.", FontSize = 13, TextColor = Color.FromArgb("#DC2626"), Padding = new Thickness(18, 14) });
            System.Diagnostics.Debug.WriteLine($"Dashboard load failed: {ex}");
        }
    }

    private static View BuildLowStockRow(DashboardLowStockItem item)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), Padding = new Thickness(18, 12), ColumnSpacing = 12, BackgroundColor = Colors.Transparent };
        var left = new VerticalStackLayout { Spacing = 2 };
        left.Children.Add(new Label { Text = item.ProductName, FontSize = 13, FontAttributes = FontAttributes.Bold });
        if (!string.IsNullOrWhiteSpace(item.SKU)) left.Children.Add(new Label { Text = $"SKU: {item.SKU}", FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });
        var right = new VerticalStackLayout { Spacing = 2, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = item.Stock.ToString(), FontSize = 14, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.End, TextColor = Color.FromArgb("#DC2626") });
        right.Children.Add(new Label { Text = $"Min {item.MinimumStock}", FontSize = 10, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.End });
        grid.Add(left, 0, 0); grid.Add(right, 1, 0);
        var tap = new TapGestureRecognizer { NumberOfTapsRequired = 1 };
        tap.Tapped += (_, _) => MainContentNavigator.Request(MainContentNavigator.Inventory);
        grid.GestureRecognizers.Add(tap);
        return grid;
    }

    private static string ResolveCurrencySymbol(string? snapshotSymbol, string? companySymbol) => !string.IsNullOrWhiteSpace(companySymbol) ? companySymbol.Trim() : !string.IsNullOrWhiteSpace(snapshotSymbol) ? snapshotSymbol.Trim() : "₹";
    private static string FormatMoney(string symbol, decimal amount) => $"{symbol}{amount:N0}";

    private void BuildCompanySection(CompanyModel? company, UserModel? user, SubscriptionModel? subscription)
    {
        CompanyDetailsHost.Children.Clear(); if (subscription?.IsExpired == true) CompanyDetailsHost.Children.Add(SubscriptionRestrictionView.Create(SubscriptionMessages.FreeTrialExpired)); CompanyDetailsHost.Children.Add(BuildCompanyDetailsCard(company, user, subscription));
    }
    private static string FormatSubscription(SubscriptionModel? subscription) => subscription == null ? "No data" : subscription.IsExpired && subscription.IsTrial ? SubscriptionMessages.FreeTrialExpired : subscription.IsTrial ? $"{subscription.PlanName} — 15-day free trial (ends {subscription.ExpiryDate:dd MMM yyyy})" : $"{subscription.PlanName} — {subscription.StatusLabel}";
    private static View BuildCompanyDetailsCard(CompanyModel? company, UserModel? user, SubscriptionModel? subscription)
    {
        var card = new Border { Style = ResolveStyle("AppCard"), Padding = 18 }; var stack = new VerticalStackLayout { Spacing = 10 }; stack.Children.Add(new Label { Text = "Company Details", FontSize = 15, FontAttributes = FontAttributes.Bold });
        if (company == null) { stack.Children.Add(new Label { Text = "No company data", FontSize = 13, TextColor = Color.FromArgb("#64748B") }); card.Content = stack; return card; }
        AddDetail(stack, "Company Name", company.Name); AddDetail(stack, "Company Code", company.CompanyCode); AddDetail(stack, "Owner", string.IsNullOrWhiteSpace(company.OwnerName) ? user?.Name : company.OwnerName); AddDetail(stack, "Mobile", company.Phone); AddDetail(stack, "Email", company.Email); AddDetail(stack, "Address", FormatAddress(company)); AddDetail(stack, "GST Number", company.TaxNumber); AddDetail(stack, "PAN Number", company.RegistrationNo); AddDetail(stack, "Subscription", FormatSubscription(subscription)); card.Content = stack; return card;
    }
    private static void AddDetail(VerticalStackLayout stack, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return; var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new GridLength(120)), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 8 }; row.Add(new Label { Text = label, FontSize = 12, TextColor = Color.FromArgb("#64748B") }, 0, 0); row.Add(new Label { Text = value, FontSize = 13, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap }, 1, 0); stack.Children.Add(row);
    }
    private static string FormatAddress(CompanyModel company) => string.Join(", ", new[] { company.Address, company.City, company.State, company.PinCode, company.Country }.Where(p => !string.IsNullOrWhiteSpace(p)));
    private static Style? ResolveStyle(string key) => Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Style style ? style : null;

    private static View BuildInvoiceRow(DashboardTransactionItem item)
    {
        var statusColor = item.Status switch { InvoiceStatus.Paid => "#059669", InvoiceStatus.Overdue => "#DC2626", InvoiceStatus.Sent => "#2563EB", InvoiceStatus.Draft => "#6B7280", InvoiceStatus.Cancelled => "#9CA3AF", _ => "#D97706" }; var statusBg = item.Status switch { InvoiceStatus.Paid => "#ECFDF5", InvoiceStatus.Overdue => "#FEF2F2", InvoiceStatus.Sent => "#EFF6FF", _ => "#F9FAFB" };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), Padding = new Thickness(18, 12), ColumnSpacing = 12, BackgroundColor = Colors.Transparent }; var left = new VerticalStackLayout { Spacing = 3 }; left.Children.Add(new Label { Text = item.InvoiceNumber, FontSize = 13, FontAttributes = FontAttributes.Bold }); left.Children.Add(new Label { Text = item.CustomerName, FontSize = 12, TextColor = Color.FromArgb("#64748B") }); left.Children.Add(new Label { Text = item.DateText, FontSize = 11, TextColor = Color.FromArgb("#94A3B8") }); var right = new VerticalStackLayout { Spacing = 3, HorizontalOptions = LayoutOptions.End }; right.Children.Add(new Label { Text = item.AmountText, FontSize = 13, FontAttributes = FontAttributes.Bold }); var badge = new Border { BackgroundColor = Color.FromArgb(statusBg), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) }, Padding = new Thickness(8, 2) }; badge.Content = new Label { Text = item.StatusLabel, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(statusColor) }; right.Children.Add(badge); grid.Add(left, 0, 0); grid.Add(right, 1, 0); var tap = new TapGestureRecognizer { NumberOfTapsRequired = 1 }; tap.Tapped += async (_, _) => await OpenInvoiceAsync(item.Id); grid.GestureRecognizers.Add(tap); return grid;
    }
}
