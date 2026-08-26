using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Billing;

[QueryProperty(nameof(InvoiceId), "id")]
public partial class InvoiceViewPage : ContentPage
{
    private readonly IBillingService _svc;
    private InvoiceModel? _invoice;
    public string? InvoiceId { get; set; }
    public Action? CloseRequested { get; set; }
    public Func<Guid, Task>? EditRequested { get; set; }

    public InvoiceViewPage(IBillingService svc) { InitializeComponent(); _svc = svc; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(InvoiceId) && Guid.TryParse(InvoiceId, out var id)) await LoadAsync(id);
    }

    public async Task LoadAsync(Guid id)
    {
        _invoice = await _svc.GetByIdAsync(id);
        if (_invoice != null) BuildContent(_invoice);
    }

    private async Task CloseAsync()
    {
        if (CloseRequested != null) { CloseRequested.Invoke(); return; }
        await Shell.Current.GoToAsync("..");
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await CloseAsync();

    private void BuildContent(InvoiceModel inv)
    {
        Content.Children.Clear();
        var actions = new HorizontalStackLayout { Spacing = 8 };
        AddAction(actions, "Edit", async () =>
        {
            if (EditRequested != null) await EditRequested(inv.LocalId);
        });
        AddAction(actions, "Print", async () => await DisplayAlertAsync("Print", "Print functionality will be connected to the invoice print service next.", "OK"));
        AddAction(actions, "Share", async () => await DisplayAlertAsync("Share", "Share functionality will be connected to the invoice sharing service next.", "OK"));
        AddAction(actions, "Record Payment", async () => await DisplayAlertAsync("Payment", "Payment recording will be connected with the Payment module.", "OK"), "SmallButton");
        if (inv.Status != InvoiceStatus.Cancelled) AddAction(actions, "Cancel Invoice", CancelInvoice, "DangerButton");
        Content.Children.Add(actions);

        var (statusBg, statusColor) = inv.Status switch
        {
            InvoiceStatus.Paid => ("#ECFDF5", "#059669"), InvoiceStatus.Overdue => ("#FEF2F2", "#DC2626"),
            InvoiceStatus.Sent => ("#EFF6FF", "#2563EB"), InvoiceStatus.PartialPaid => ("#FFFBEB", "#D97706"), _ => ("#F9FAFB", "#6B7280")
        };
        var headerCard = new Border { Style = (Style)Resources["AppCard"] };
        var hGrid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)) };
        hGrid.Add(new Label { Text = inv.InvoiceNumber, FontSize = 20, FontAttributes = FontAttributes.Bold }, 0, 0);
        var badge = new Border { BackgroundColor = Color.FromArgb(statusBg), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) }, Padding = new Thickness(10, 4), Content = new Label { Text = inv.StatusLabel, FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(statusColor) } };
        hGrid.Add(badge, 1, 0);
        var customerLabel = new Label { Text = inv.CustomerName, FontSize = 15, TextColor = Color.FromArgb("#64748B"), Margin = new Thickness(0, 6, 0, 0) };
        hGrid.Add(customerLabel, 0, 1); Grid.SetColumnSpan(customerLabel, 2); headerCard.Content = hGrid; Content.Children.Add(headerCard);

        var amtCard = new Border { Style = (Style)Resources["AppCard"] };
        var amtGrid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 12 };
        AddAmount(amtGrid, 0, "Grand Total", $"₹{inv.GrandTotal:N2}", "#0F172A"); AddAmount(amtGrid, 1, "Paid", $"₹{inv.PaidAmount:N2}", "#059669"); AddAmount(amtGrid, 2, "Due", $"₹{inv.DueAmount:N2}", inv.DueAmount > 0 ? "#DC2626" : "#059669");
        amtCard.Content = amtGrid; Content.Children.Add(amtCard);

        var detailCard = new Border { Style = (Style)Resources["AppCard"] }; var details = new VerticalStackLayout { Spacing = 12 };
        details.Children.Add(new Label { Text = "Sale Details", FontSize = 14, FontAttributes = FontAttributes.Bold }); details.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });
        AddDetail(details, "Invoice Date", inv.InvoiceDate.ToString("dd MMM yyyy")); AddDetail(details, "Due Date", inv.DueDate.ToString("dd MMM yyyy")); AddDetail(details, "Payment", inv.PaymentMethod.ToString()); AddDetail(details, "Customer", inv.CustomerName);
        if (!string.IsNullOrWhiteSpace(inv.Notes)) AddDetail(details, "Notes", inv.Notes); detailCard.Content = details; Content.Children.Add(detailCard);

        var lineCard = new Border { Style = (Style)Resources["AppCard"] }; var lines = new VerticalStackLayout { Spacing = 10 };
        lines.Children.Add(new Label { Text = "Sale Items", FontSize = 14, FontAttributes = FontAttributes.Bold }); lines.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });
        foreach (var item in inv.Items)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
            var info = new VerticalStackLayout { Spacing = 2 }; info.Children.Add(new Label { Text = item.ProductName, FontSize = 13, FontAttributes = FontAttributes.Bold }); info.Children.Add(new Label { Text = $"{item.Quantity} × ₹{item.UnitPrice:N2}  •  SKU: {item.SKU}", FontSize = 12, TextColor = Color.FromArgb("#64748B") });
            row.Add(info, 0, 0); row.Add(new Label { Text = $"₹{item.GrandTotal:N2}", FontSize = 14, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }, 1, 0); lines.Children.Add(row);
        }
        lineCard.Content = lines; Content.Children.Add(lineCard);
    }

    private static void AddAction(HorizontalStackLayout host, string text, Func<Task> action, string style = "SmallSecondaryButton")
    {
        var button = new Button { Text = text, Style = (Style)Application.Current!.Resources[style], HeightRequest = 36, Padding = new Thickness(14, 0) };
        button.Clicked += async (_, _) => await action(); host.Children.Add(button);
    }

    private static void AddAmount(Grid grid, int column, string label, string value, string color)
    {
        var box = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center };
        box.Children.Add(new Label { Text = value, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(color), HorizontalOptions = LayoutOptions.Center });
        box.Children.Add(new Label { Text = label, FontSize = 11, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center }); grid.Add(box, column, 0);
    }

    private static void AddDetail(VerticalStackLayout host, string label, string value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new GridLength(130)), new ColumnDefinition(GridLength.Star)) };
        row.Add(new Label { Text = label, FontSize = 13, TextColor = Color.FromArgb("#64748B") }, 0, 0); row.Add(new Label { Text = value, FontSize = 13, FontAttributes = FontAttributes.Bold }, 1, 0); host.Children.Add(row);
    }

    private async Task CancelInvoice()
    {
        if (_invoice == null) return;
        if (!await DialogHelper.ConfirmAsync("Cancel Invoice", "Are you sure you want to cancel this sale?", "Cancel Sale", "Keep")) return;
        var result = await _svc.CancelAsync(_invoice.LocalId);
        if (!result.Ok) { await DisplayAlertAsync("Unable to cancel", result.Error ?? "Unable to cancel sale.", "OK"); return; }
        await CloseAsync();
    }
}
