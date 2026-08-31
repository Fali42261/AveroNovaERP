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
    public Func<Guid, Task>? RecordPaymentRequested { get; set; }

    public InvoiceViewPage(IBillingService svc) { InitializeComponent(); _svc = svc; }
    protected override async void OnAppearing() { base.OnAppearing(); if (!string.IsNullOrEmpty(InvoiceId) && Guid.TryParse(InvoiceId, out var id)) await LoadAsync(id); }
    public async Task LoadAsync(Guid id) { _invoice = await _svc.GetByIdAsync(id); if (_invoice != null) BuildContent(_invoice); }
    private async Task CloseAsync() { if (CloseRequested != null) { CloseRequested.Invoke(); return; } await Shell.Current.GoToAsync(".."); }
    private async void OnBackClicked(object? sender, EventArgs e) => await CloseAsync();

    private void BuildContent(InvoiceModel inv)
    {
        Content.Children.Clear();
        var actions = new HorizontalStackLayout { Spacing = 8 };
        if (inv.Status != InvoiceStatus.Cancelled)
            AddAction(actions, "Edit", async () => { if (EditRequested != null) await EditRequested(inv.LocalId); });
        AddAction(actions, "Copy Invoice", async () => await CopyInvoiceAsync(inv));
        AddAction(actions, "Share", async () => await ShareInvoiceAsync(inv));
        if (inv.Status is not (InvoiceStatus.Draft or InvoiceStatus.Paid or InvoiceStatus.Cancelled) && inv.DueAmount > 0)
            AddAction(actions, "Record Payment", async () => { if (RecordPaymentRequested != null) await RecordPaymentRequested(inv.LocalId); }, "SmallButton");
        if (inv.Status != InvoiceStatus.Cancelled)
            AddAction(actions, "Cancel Invoice", CancelInvoice, "DangerButton");
        Content.Children.Add(actions);

        var effectiveStatus = inv.Status == InvoiceStatus.Sent && inv.DueAmount > 0 && inv.DueDate.Date < DateTime.Today ? InvoiceStatus.Overdue : inv.Status;
        var statusLabel = effectiveStatus == InvoiceStatus.Overdue ? "Overdue" : inv.StatusLabel;
        var (statusBg, statusColor) = effectiveStatus switch
        {
            InvoiceStatus.Paid => ("#ECFDF5", "#059669"),
            InvoiceStatus.Overdue => ("#FEF2F2", "#DC2626"),
            InvoiceStatus.Sent => ("#EFF6FF", "#2563EB"),
            InvoiceStatus.PartialPaid => ("#FFFBEB", "#D97706"),
            _ => ("#F9FAFB", "#6B7280")
        };

        var headerCard = new Border { Style = (Style)Resources["AppCard"] };
        var hGrid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)) };
        hGrid.Add(new Label { Text = inv.InvoiceNumber, FontSize = 20, FontAttributes = FontAttributes.Bold }, 0, 0);
        hGrid.Add(new Border { BackgroundColor = Color.FromArgb(statusBg), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) }, Padding = new Thickness(10, 4), Content = new Label { Text = statusLabel, FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(statusColor) } }, 1, 0);
        var customerLabel = new Label { Text = inv.CustomerName, FontSize = 15, TextColor = Color.FromArgb("#64748B"), Margin = new Thickness(0, 6, 0, 0) };
        hGrid.Add(customerLabel, 0, 1); Grid.SetColumnSpan(customerLabel, 2); headerCard.Content = hGrid; Content.Children.Add(headerCard);

        var amtCard = new Border { Style = (Style)Resources["AppCard"] };
        var amtGrid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 12 };
        AddAmount(amtGrid, 0, "Grand Total", $"₹{inv.GrandTotal:N2}", "#0F172A");
        AddAmount(amtGrid, 1, "Paid", $"₹{inv.PaidAmount:N2}", "#059669");
        AddAmount(amtGrid, 2, "Due", $"₹{Math.Max(0, inv.DueAmount):N2}", inv.DueAmount > 0 ? "#DC2626" : "#059669");
        amtCard.Content = amtGrid; Content.Children.Add(amtCard);

        var detailCard = new Border { Style = (Style)Resources["AppCard"] };
        var details = new VerticalStackLayout { Spacing = 12 };
        details.Children.Add(new Label { Text = "Sale Details", FontSize = 14, FontAttributes = FontAttributes.Bold });
        details.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });
        AddDetail(details, "Invoice Date", inv.InvoiceDate.ToString("dd MMM yyyy"));
        AddDetail(details, "Due Date", inv.DueDate.ToString("dd MMM yyyy"));
        AddDetail(details, "Payment", PaymentLabel(inv.PaymentMethod));
        AddDetail(details, "Customer", inv.CustomerName);
        AddDetail(details, "Subtotal", $"₹{inv.Subtotal:N2}");
        AddDetail(details, "Tax", $"₹{inv.TaxTotal:N2}");
        AddDetail(details, "Discount", $"₹{inv.DiscountAmount:N2}");
        if (!string.IsNullOrWhiteSpace(inv.Notes)) AddDetail(details, "Notes", inv.Notes);
        detailCard.Content = details; Content.Children.Add(detailCard);

        var lineCard = new Border { Style = (Style)Resources["AppCard"] };
        var lines = new VerticalStackLayout { Spacing = 10 };
        lines.Children.Add(new Label { Text = "Sale Items", FontSize = 14, FontAttributes = FontAttributes.Bold });
        lines.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });
        foreach (var item in inv.Items)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
            var info = new VerticalStackLayout { Spacing = 2 };
            info.Children.Add(new Label { Text = item.ProductName, FontSize = 13, FontAttributes = FontAttributes.Bold });
            info.Children.Add(new Label { Text = $"{item.Quantity} × ₹{item.UnitPrice:N2} • SKU: {item.SKU} • GST {item.TaxPct:0.##}% • Disc {item.DiscountPct:0.##}%", FontSize = 12, TextColor = Color.FromArgb("#64748B") });
            row.Add(info, 0, 0);
            row.Add(new Label { Text = $"₹{item.GrandTotal:N2}", FontSize = 14, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }, 1, 0);
            lines.Children.Add(row);
        }
        lineCard.Content = lines; Content.Children.Add(lineCard);
    }

    private async Task CopyInvoiceAsync(InvoiceModel inv)
    {
        await Clipboard.Default.SetTextAsync(BuildInvoiceText(inv));
        await DisplayAlertAsync("Invoice copied", "Invoice details are copied to the clipboard.", "OK");
    }

    private static Task ShareInvoiceAsync(InvoiceModel inv)
        => Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = $"Invoice {inv.InvoiceNumber}",
            Subject = $"Invoice {inv.InvoiceNumber}",
            Text = BuildInvoiceText(inv)
        });

    private static string BuildInvoiceText(InvoiceModel inv)
    {
        var lines = new List<string>
        {
            $"Invoice: {inv.InvoiceNumber}",
            $"Customer: {inv.CustomerName}",
            $"Date: {inv.InvoiceDate:dd MMM yyyy}",
            $"Due: {inv.DueDate:dd MMM yyyy}",
            string.Empty,
            "Items:"
        };
        lines.AddRange(inv.Items.Select(x => $"- {x.ProductName} | {x.Quantity} × ₹{x.UnitPrice:N2} = ₹{x.GrandTotal:N2}"));
        lines.Add(string.Empty);
        lines.Add($"Subtotal: ₹{inv.Subtotal:N2}");
        lines.Add($"Tax: ₹{inv.TaxTotal:N2}");
        lines.Add($"Discount: ₹{inv.DiscountAmount:N2}");
        lines.Add($"Grand Total: ₹{inv.GrandTotal:N2}");
        lines.Add($"Paid: ₹{inv.PaidAmount:N2}");
        lines.Add($"Due: ₹{Math.Max(0, inv.DueAmount):N2}");
        return string.Join(Environment.NewLine, lines);
    }

    private static void AddAction(HorizontalStackLayout host, string text, Func<Task> action, string style = "SmallSecondaryButton")
    {
        var button = new Button { Text = text, Style = (Style)Microsoft.Maui.Controls.Application.Current!.Resources[style], HeightRequest = 36, Padding = new Thickness(14, 0) };
        button.Clicked += async (_, _) => await action();
        host.Children.Add(button);
    }

    private static void AddAmount(Grid grid, int column, string label, string value, string color)
    {
        var box = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center };
        box.Children.Add(new Label { Text = value, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(color), HorizontalOptions = LayoutOptions.Center });
        box.Children.Add(new Label { Text = label, FontSize = 11, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center });
        grid.Add(box, column, 0);
    }

    private static void AddDetail(VerticalStackLayout host, string label, string value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(new GridLength(18)), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 8 };
        row.Add(new Label { Text = label, FontSize = 13, TextColor = Color.FromArgb("#64748B") }, 0, 0);
        row.Add(new Label { Text = ":", FontSize = 13, TextColor = Color.FromArgb("#64748B") }, 1, 0);
        row.Add(new Label { Text = value, FontSize = 13, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap }, 2, 0);
        host.Children.Add(row);
    }

    private static string PaymentLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.BankTransfer => "Bank Transfer",
        PaymentMethod.CreditCard => "Credit Card",
        PaymentMethod.DebitCard => "Debit Card",
        PaymentMethod.Cheque => "Cheque",
        PaymentMethod.Online => "Online",
        _ => "Cash"
    };

    private async Task CancelInvoice()
    {
        if (_invoice == null) return;
        if (!await DialogHelper.ConfirmAsync("Cancel Invoice", "Are you sure you want to cancel this sale? Stock will be restored.", "Cancel Sale", "Keep")) return;
        var result = await _svc.CancelAsync(_invoice.LocalId);
        if (!result.Ok) { await DisplayAlertAsync("Unable to cancel", result.Error ?? "Unable to cancel sale.", "OK"); return; }
        await CloseAsync();
    }
}
