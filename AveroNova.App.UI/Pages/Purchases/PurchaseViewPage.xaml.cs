using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Purchases;

[QueryProperty(nameof(PurchaseId), "id")]
public partial class PurchaseViewPage : ContentPage
{
    private readonly IPurchaseService _svc;
    private PurchaseModel? _purchase;
    public string? PurchaseId { get; set; }
    public Action? CloseRequested { get; set; }
    public Func<Guid, Task>? EditRequested { get; set; }

    public PurchaseViewPage(IPurchaseService svc) { InitializeComponent(); _svc = svc; }
    protected override async void OnAppearing() { base.OnAppearing(); if (!string.IsNullOrEmpty(PurchaseId) && Guid.TryParse(PurchaseId, out var id)) await LoadAsync(id); }
    public async Task LoadAsync(Guid id) { _purchase = await _svc.GetByIdAsync(id); if (_purchase != null) BuildContent(_purchase); }

    private void BuildContent(PurchaseModel p)
    {
        Content.Children.Clear();
        var card = new Border { Style = (Style)Resources["AppCard"] }; var vsl = new VerticalStackLayout { Spacing = 12 };
        vsl.Children.Add(new Label { Text = p.PurchaseNumber, FontSize = 20, FontAttributes = FontAttributes.Bold });
        vsl.Children.Add(new Label { Text = p.SupplierName, FontSize = 15, TextColor = Color.FromArgb("#64748B") }); vsl.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });
        void Row(string l, string v) { var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new GridLength(130)), new ColumnDefinition(GridLength.Star)) }; g.Add(new Label { Text = l, FontSize = 13, TextColor = Color.FromArgb("#64748B") }, 0, 0); g.Add(new Label { Text = v, FontSize = 13, FontAttributes = FontAttributes.Bold }, 1, 0); vsl.Children.Add(g); }
        Row("Status", p.StatusLabel); Row("Date", p.PurchaseDate.ToString("dd MMM yyyy")); Row("Due Date", p.DueDate.ToString("dd MMM yyyy")); Row("Payment", p.PaymentMethod.ToString());
        Row("Reference", p.Reference); Row("Grand Total", $"₹{p.GrandTotal:N2}"); Row("Paid", $"₹{p.PaidAmount:N2}"); Row("Due", $"₹{p.DueAmount:N2}"); if (!string.IsNullOrEmpty(p.Notes)) Row("Notes", p.Notes);
        card.Content = vsl; Content.Children.Add(card);

        var itemCard = new Border { Style = (Style)Resources["AppCard"] }; var items = new VerticalStackLayout { Spacing = 10 };
        items.Children.Add(new Label { Text = "Purchase Items", FontSize = 14, FontAttributes = FontAttributes.Bold }); items.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });
        foreach (var item in p.Items)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };
            var info = new VerticalStackLayout { Spacing = 2 }; info.Children.Add(new Label { Text = item.ProductName, FontSize = 13, FontAttributes = FontAttributes.Bold }); info.Children.Add(new Label { Text = $"{item.Quantity} × ₹{item.UnitPrice:N2} • Tax {item.TaxPct:0.##}% • {item.SKU}", FontSize = 11, TextColor = Color.FromArgb("#64748B") });
            g.Add(info, 0, 0); g.Add(new Label { Text = $"₹{item.GrandTotal:N2}", FontSize = 13, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }, 1, 0); items.Children.Add(g);
        }
        itemCard.Content = items; Content.Children.Add(itemCard);

        var deleteBtn = new Button { Text = "Delete Purchase", Style = (Style)Resources["DangerButton"], HorizontalOptions = LayoutOptions.End, HeightRequest = 40, Padding = new Thickness(16, 0) };
        deleteBtn.Clicked += async (_, _) =>
        {
            if (!await DialogHelper.ConfirmDeleteAsync("Purchase", $"Delete {p.PurchaseNumber}?")) return;
            var result = await _svc.DeleteAsync(p.LocalId); if (!result.Ok) { await DisplayAlertAsync("Unable to delete", result.Error ?? "Delete failed.", "OK"); return; } await CloseAsync();
        };
        Content.Children.Add(deleteBtn);
    }

    private async Task CloseAsync() { if (CloseRequested != null) { CloseRequested.Invoke(); return; } await Shell.Current.GoToAsync(".."); }
    private async void OnEditClicked(object s, EventArgs e) { if (_purchase != null && EditRequested != null) await EditRequested(_purchase.LocalId); }
    private async void OnBackClicked(object s, EventArgs e) => await CloseAsync();
}
