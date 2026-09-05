using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Purchases;

[QueryProperty(nameof(PurchaseId), "id")]
public partial class PurchaseViewPage : ContentPage
{
    private readonly IPurchaseService _svc;
    private PurchaseModel? _purchase;
    public string? PurchaseId { get; set; }

    public PurchaseViewPage(IPurchaseService svc) { InitializeComponent(); _svc = svc; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(PurchaseId) && Guid.TryParse(PurchaseId, out var id))
        {
            _purchase = await _svc.GetByIdAsync(id);
            if (_purchase != null) BuildContent(_purchase);
        }
    }

    private void BuildContent(PurchaseModel p)
    {
        Content.Children.Clear();
        var card = new Border { Style = (Style)Resources["AppCard"] };
        var vsl  = new VerticalStackLayout { Spacing = 12 };
        vsl.Children.Add(new Label { Text = p.PurchaseNumber, FontSize = 20, FontAttributes = FontAttributes.Bold });
        vsl.Children.Add(new Label { Text = p.SupplierName, FontSize = 15, TextColor = Color.FromArgb("#64748B") });
        vsl.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });
        void Row(string l, string v) { var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new GridLength(130)), new ColumnDefinition(GridLength.Star)) }; g.Add(new Label { Text = l, FontSize = 13, TextColor = Color.FromArgb("#64748B") }, 0, 0); g.Add(new Label { Text = v, FontSize = 13, FontAttributes = FontAttributes.Bold }, 1, 0); vsl.Children.Add(g); }
        Row("Status",        p.StatusLabel);
        Row("Date",          p.PurchaseDate.ToString("dd MMM yyyy"));
        Row("Due Date",      p.DueDate.ToString("dd MMM yyyy"));
        Row("Reference",     p.Reference);
        Row("Grand Total",   $"${p.GrandTotal:N2}");
        Row("Paid",          $"${p.PaidAmount:N2}");
        Row("Due",           $"${p.DueAmount:N2}");
        if (!string.IsNullOrEmpty(p.Notes)) Row("Notes", p.Notes);
        card.Content = vsl;

        var deleteBtn = new Button { Text = "Delete Purchase", Style = (Style)Resources["DangerButton"], HorizontalOptions = LayoutOptions.Fill };
        deleteBtn.Clicked += async (_, _) =>
        {
            if (!await DialogHelper.ConfirmDeleteAsync("Purchase", $"Delete {p.PurchaseNumber}?")) return;
            await _svc.DeleteAsync(p.LocalId);
            await Shell.Current.GoToAsync("..");
        };

        Content.Children.Add(card);
        Content.Children.Add(deleteBtn);
    }

    private async void OnEditClicked(object s, EventArgs e) => await Shell.Current.GoToAsync($"{AppRoutes.PurchaseNew}?id={_purchase?.LocalId}");
    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");
}
