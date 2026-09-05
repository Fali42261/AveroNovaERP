using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Layouts;

namespace AveroNova.App.UI.Pages.Billing;

[QueryProperty(nameof(InvoiceId), "id")]
public partial class InvoiceViewPage : ContentPage
{
    private readonly IBillingService _svc;
    private InvoiceModel? _invoice;
    public string? InvoiceId { get; set; }

    public InvoiceViewPage(IBillingService svc) { InitializeComponent(); _svc = svc; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(InvoiceId) && Guid.TryParse(InvoiceId, out var id))
        {
            _invoice = await _svc.GetByIdAsync(id);
            if (_invoice != null) BuildContent(_invoice);
        }
    }

    private void BuildContent(InvoiceModel inv)
    {
        Content.Children.Clear();

        var actions = new FlexLayout { Wrap = FlexWrap.Wrap, Direction = FlexDirection.Row, JustifyContent = FlexJustify.Start };
        void AddAction(string label, Func<Task> onClick, string style = "SmallSecondaryButton")
        {
            var b = new Button { Text = label, Style = (Style)Resources[style], Margin = new Thickness(0, 0, 8, 8) };
            b.Clicked += async (_, _) => await onClick();
            actions.Children.Add(b);
        }
        AddAction("Edit", async () => await Shell.Current.GoToAsync($"{AppRoutes.InvoiceEdit}?id={inv.LocalId}"));
        AddAction("Print", async () => await AppToast.InfoAsync("Print will be available in the document/export module."));
        AddAction("Share", async () => await AppToast.InfoAsync("Share will be available in the document/export module."));
        AddAction("Record Payment", async () => await AppToast.InfoAsync("Open Payments to record this invoice payment."), "SmallButton");
        if (inv.Status != InvoiceStatus.Cancelled)
            AddAction("Cancel Invoice", CancelInvoice, "DangerButton");
        Content.Children.Add(actions);

        var (statusBg, statusColor) = inv.Status switch
        {
            InvoiceStatus.Paid => ("#ECFDF5", "#059669"),
            InvoiceStatus.Overdue => ("#FEF2F2", "#DC2626"),
            InvoiceStatus.Sent => ("#EFF6FF", "#2563EB"),
            _ => ("#F9FAFB", "#6B7280")
        };

        var headerCard = new Border { Style = (Style)Resources["AppCard"] };
        var hGrid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)) };
        hGrid.Add(new Label { Text = inv.InvoiceNumber, FontSize = 20, FontAttributes = FontAttributes.Bold }, 0, 0);
        var badge = new Border { BackgroundColor = Color.FromArgb(statusBg), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) }, Padding = new Thickness(10, 4) };
        badge.Content = new Label { Text = inv.StatusLabel, FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(statusColor) };
        hGrid.Add(badge, 1, 0);
        var customer = new Label { Text = inv.CustomerName, FontSize = 15, TextColor = Color.FromArgb("#64748B"), Margin = new Thickness(0, 6, 0, 0) };
        hGrid.Add(customer, 0, 1); Grid.SetColumnSpan(customer, 2);
        var sync = new Label
        {
            Text = inv.SyncStatus switch { SyncStatus.Synced => "Synced", SyncStatus.SyncFailed => "Sync failed", _ => "Pending sync" },
            FontSize = 11,
            TextColor = inv.SyncStatus == SyncStatus.SyncFailed ? Color.FromArgb("#DC2626") : Color.FromArgb("#64748B"),
            Margin = new Thickness(0, 4, 0, 0)
        };
        hGrid.Add(sync, 0, 2); Grid.SetColumnSpan(sync, 2);
        headerCard.Content = hGrid;

        var amtCard = new Border { Style = (Style)Resources["AppCard"] };
        var amtGrid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 12 };
        void AmtBox(int col, string label, string value, string color)
        {
            var v = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center };
            v.Children.Add(new Label { Text = value, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(color), HorizontalOptions = LayoutOptions.Center });
            v.Children.Add(new Label { Text = label, FontSize = 11, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center });
            amtGrid.Add(v, col, 0);
        }
        AmtBox(0, "Grand Total", $"${inv.GrandTotal:N2}", "#0F172A");
        AmtBox(1, "Paid", $"${inv.PaidAmount:N2}", "#059669");
        AmtBox(2, "Due", $"${inv.DueAmount:N2}", inv.DueAmount > 0 ? "#DC2626" : "#059669");
        amtCard.Content = amtGrid;

        var detailCard = new Border { Style = (Style)Resources["AppCard"] };
        var dv = new VerticalStackLayout { Spacing = 12 };
        dv.Children.Add(new Label { Text = "Invoice Details", FontSize = 14, FontAttributes = FontAttributes.Bold });
        dv.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });
        void DRow(string l, string v) { var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new GridLength(130)), new ColumnDefinition(GridLength.Star)) }; g.Add(new Label { Text = l, FontSize = 13, TextColor = Color.FromArgb("#64748B") }, 0, 0); g.Add(new Label { Text = v, FontSize = 13, FontAttributes = FontAttributes.Bold }, 1, 0); dv.Children.Add(g); }
        DRow("Invoice Date", inv.InvoiceDate.ToString("dd MMM yyyy"));
        DRow("Due Date", inv.DueDate.ToString("dd MMM yyyy"));
        DRow("Payment", inv.PaymentMethod.ToString());
        if (!string.IsNullOrEmpty(inv.Notes)) DRow("Notes", inv.Notes);
        detailCard.Content = dv;

        if (inv.Items.Count > 0)
        {
            var lineCard = new Border { Style = (Style)Resources["AppCard"] };
            var lv = new VerticalStackLayout { Spacing = 10 };
            lv.Children.Add(new Label { Text = "Line Items", FontSize = 14, FontAttributes = FontAttributes.Bold });
            lv.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });
            foreach (var item in inv.Items)
            {
                var ig = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
                var il = new VerticalStackLayout { Spacing = 2 };
                il.Children.Add(new Label { Text = item.ProductName, FontSize = 13, FontAttributes = FontAttributes.Bold });
                il.Children.Add(new Label { Text = $"{item.Quantity} × ${item.UnitPrice:N2}", FontSize = 12, TextColor = Color.FromArgb("#64748B") });
                ig.Add(il, 0, 0);
                ig.Add(new Label { Text = $"${item.GrandTotal:N2}", FontSize = 14, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }, 1, 0);
                lv.Children.Add(ig);
            }
            lineCard.Content = lv;
            Content.Children.Add(lineCard);
        }

        Content.Children.Add(headerCard);
        Content.Children.Add(amtCard);
        Content.Children.Add(detailCard);
    }

    private async Task CancelInvoice()
    {
        if (_invoice == null) return;
        if (!await DialogHelper.ConfirmAsync("Cancel Invoice", "Are you sure you want to cancel this invoice?", "Cancel Invoice", "Keep")) return;
        var (ok, error) = await _svc.CancelAsync(_invoice.LocalId);
        if (!ok)
        {
            await AppToast.ErrorAsync(error ?? "Unable to cancel invoice.");
            return;
        }
        await AppToast.SuccessAsync("Invoice cancelled successfully.");
        await Shell.Current.GoToAsync("..");
    }

    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");
}
