using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Purchases;

[QueryProperty(nameof(PurchaseId), "id")]
public partial class PurchaseViewPage : ContentPage, IHostedPage
{
    private readonly IPurchaseService _svc;
    private readonly IMainContentNavigator _navigator;
    private readonly Func<PurchaseFormPage> _formFactory;
    private PurchaseModel? _purchase;
    public string? PurchaseId { get; set; }

    public PurchaseViewPage(IPurchaseService svc, IMainContentNavigator navigator, Func<PurchaseFormPage> formFactory) { InitializeComponent(); _svc = svc; _navigator=navigator; _formFactory=formFactory; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }
    public async Task LoadForHostAsync()
    {
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
        foreach (var item in p.Items) Row(item.ProductName, $"{item.Quantity} × ${item.UnitPrice:N2} + {item.TaxPct:N2}% = ${item.GrandTotal:N2}");
        if (!string.IsNullOrEmpty(p.Notes)) Row("Notes", p.Notes);
        card.Content = vsl;

        var deleteBtn = new Button { Text = "Delete Purchase", Style = (Style)Resources["DangerButton"], HorizontalOptions = LayoutOptions.Fill };
        deleteBtn.Clicked += async (_, _) =>
        {
            if (!await DialogHelper.ConfirmDeleteAsync("Purchase", $"Delete {p.PurchaseNumber}?")) return;
            var (ok,error)=await _svc.DeleteAsync(p.LocalId);
            if(!ok) { await DisplayAlert("Delete failed",error ?? "Unable to delete purchase.","OK"); return; }
            await _navigator.GoBackAsync();
        };

        Content.Children.Add(card);
        Content.Children.Add(deleteBtn);
    }

    private async void OnEditClicked(object s, EventArgs e) { if(_purchase is null)return; var page=_formFactory(); page.EditId=_purchase.LocalId.ToString("D"); await _navigator.NavigateAsync(page,"Edit Purchase","Home / Purchases / Edit"); }
    private async void OnBackClicked(object s, EventArgs e) => await _navigator.GoBackAsync();
}
