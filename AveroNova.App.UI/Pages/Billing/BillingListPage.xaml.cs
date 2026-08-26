using AveroNova.App.UI.Models;
using AveroNova.App.UI.Pages.Payments;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Billing;

public partial class BillingListPage : ContentPage
{
    private readonly IBillingService _svc;
    private readonly ICompanyService _company;
    private readonly ICustomerService _customers;
    private readonly IProductService _products;
    private readonly IPaymentService _payments;
    private readonly IPurchaseService _purchases;
    private List<InvoiceModel> _all = [];
    private string _filter = "All";

    public BillingListPage(IBillingService svc, ICompanyService company, ICustomerService customers, IProductService products, IPaymentService payments, IPurchaseService purchases)
    {
        InitializeComponent(); _svc = svc; _company = company; _customers = customers; _products = products; _payments = payments; _purchases = purchases; BuildFilterTabs();
    }

    public Task ReloadAsync() => LoadAsync();
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async void OnRefreshing(object s, EventArgs e) { await LoadAsync(); Refresher.IsRefreshing = false; }

    private void BuildFilterTabs()
    {
        FilterTabs.Children.Clear();
        var statuses = new[] { "All", "Draft", "Sent", "Partial", "Paid", "Overdue", "Cancelled" };
        foreach (var st in statuses)
        {
            var btn = new Button
            {
                Text = st,
                FontSize = 12,
                HeightRequest = 34,
                Padding = new Thickness(14, 0),
                CornerRadius = 17,
                BorderWidth = 1,
                BorderColor = Color.FromArgb("#E2E8F0"),
                BackgroundColor = st == _filter ? Color.FromArgb("#2563EB") : Colors.Transparent,
                TextColor = st == _filter ? Colors.White : Color.FromArgb("#64748B")
            };
            var captured = st;
            btn.Clicked += async (_, _) => { _filter = captured; BuildFilterTabs(); await LoadAsync(); };
            FilterTabs.Children.Add(btn);
        }
    }

    private async Task LoadAsync()
    {
        _all = await _svc.GetAllAsync(_company.CurrentCompany?.LocalId ?? Guid.Empty);
        var shown = _filter == "All" ? _all : _all.Where(i => EffectiveStatusLabel(i) == _filter).ToList();
        LblCount.Text = $"{shown.Count} sale{(shown.Count == 1 ? "" : "s")}";
        InvoiceList.Children.Clear();
        if (shown.Count == 0)
        {
            InvoiceList.Children.Add(new Label { Text = "No sales found.", FontSize = 14, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40) });
            return;
        }
        foreach (var inv in shown.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.CreatedAt)) InvoiceList.Children.Add(BuildRow(inv));
    }

    private View BuildRow(InvoiceModel inv)
    {
        var effectiveStatus = EffectiveStatus(inv);
        var statusLabel = EffectiveStatusLabel(inv);
        var (statusBg, statusColor) = effectiveStatus switch
        {
            InvoiceStatus.Paid => ("#ECFDF5", "#059669"),
            InvoiceStatus.Overdue => ("#FEF2F2", "#DC2626"),
            InvoiceStatus.Sent => ("#EFF6FF", "#2563EB"),
            InvoiceStatus.Draft => ("#F9FAFB", "#6B7280"),
            InvoiceStatus.PartialPaid => ("#FFFBEB", "#D97706"),
            InvoiceStatus.Cancelled => ("#F3F4F6", "#9CA3AF"),
            _ => ("#F3F4F6", "#9CA3AF")
        };
        var border = new Border
        {
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E293B") : Colors.White,
            Stroke = Color.FromArgb("#E2E8F0"), StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Padding = new Thickness(14, 12)
        };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };
        var left = new VerticalStackLayout { Spacing = 4 };
        left.Children.Add(new Label { Text = inv.InvoiceNumber, FontSize = 14, FontAttributes = FontAttributes.Bold });
        left.Children.Add(new Label { Text = inv.CustomerName, FontSize = 13, TextColor = Color.FromArgb("#64748B") });
        left.Children.Add(new Label { Text = inv.InvoiceDate.ToString("dd MMM yyyy") + $" • Due: {inv.DueDate:dd MMM yyyy}", FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });
        var right = new VerticalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = $"₹{inv.GrandTotal:N2}", FontSize = 15, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.End });
        right.Children.Add(new Border { BackgroundColor = Color.FromArgb(statusBg), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) }, Padding = new Thickness(8, 3), Content = new Label { Text = statusLabel, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(statusColor) } });
        if (inv.DueAmount > 0 && inv.Status != InvoiceStatus.Cancelled) right.Children.Add(new Label { Text = $"Due: ₹{inv.DueAmount:N2}", FontSize = 11, TextColor = Color.FromArgb("#D97706"), HorizontalOptions = LayoutOptions.End });
        var viewBtn = new Button { Text = "View", Style = TryStyle("SmallSecondaryButton"), HeightRequest = 36, Padding = new Thickness(14, 0) };
        viewBtn.Clicked += async (_, _) => await OpenViewAsync(inv.LocalId);
        right.Children.Add(viewBtn);
        grid.Add(left, 0, 0); grid.Add(right, 1, 0); border.Content = grid; return border;
    }

    private static InvoiceStatus EffectiveStatus(InvoiceModel inv)
        => inv.Status == InvoiceStatus.Sent && inv.DueAmount > 0 && inv.DueDate.Date < DateTime.Today ? InvoiceStatus.Overdue : inv.Status;

    private static string EffectiveStatusLabel(InvoiceModel inv)
        => EffectiveStatus(inv) == InvoiceStatus.Overdue ? "Overdue" : inv.StatusLabel;

    private async Task OpenNewAsync()
    {
        var page = new InvoiceFormPage(_svc, _customers, _products, _company) { CloseRequested = CloseActionOverlay };
        await page.LoadForNewAsync(); ShowActionPage(page);
    }

    private async Task OpenEditAsync(Guid id)
    {
        var page = new InvoiceFormPage(_svc, _customers, _products, _company) { CloseRequested = CloseActionOverlay };
        await page.LoadForEditAsync(id); ShowActionPage(page);
    }

    private async Task OpenPaymentAsync(Guid invoiceId)
    {
        var page = new PaymentFormPage(_payments, _company, _svc, _purchases) { CloseRequested = CloseActionOverlay };
        await page.LoadForInvoiceAsync(invoiceId); ShowActionPage(page);
    }

    private async Task OpenViewAsync(Guid id)
    {
        var page = new InvoiceViewPage(_svc) { CloseRequested = CloseActionOverlay, EditRequested = OpenEditAsync, RecordPaymentRequested = OpenPaymentAsync };
        await page.LoadAsync(id); ShowActionPage(page);
    }

    private void ShowActionPage(ContentPage page)
    {
        var content = page.Content; if (content == null) return;
        page.Content = null; ActionContent.Content = content; ActionOverlay.IsVisible = true;
    }

    private void CloseActionOverlay()
    {
        ActionContent.Content = null; ActionOverlay.IsVisible = false; _ = LoadAsync();
    }

    private async void OnNewClicked(object s, EventArgs e) => await OpenNewAsync();
    private static Style? TryStyle(string key) => Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Style style ? style : null;
}
