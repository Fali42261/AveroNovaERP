using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Products;

[QueryProperty(nameof(ProductId), "id")]
public partial class ProductViewPage : ContentPage
{
    private readonly IProductService _svc;
    private ProductModel? _product;
    public string? ProductId { get; set; }

    public ProductViewPage(IProductService svc) { InitializeComponent(); _svc = svc; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(ProductId) && Guid.TryParse(ProductId, out var id))
        {
            _product = await _svc.GetByIdAsync(id);
            if (_product != null) BuildContent(_product);
        }
    }

    private void BuildContent(ProductModel p)
    {
        Content.Children.Clear();

        // Stock status
        if (p.IsLowStock)
        {
            var warn = new Border { BackgroundColor = Color.FromArgb("#FEF2F2"), Stroke = Color.FromArgb("#FECACA"), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) }, Padding = new Thickness(14, 10) };
            warn.Content = new Label { Text = $"⚠  Low Stock Warning: Only {p.Stock} units remaining. Minimum: {p.MinimumStock}", FontSize = 13, TextColor = Color.FromArgb("#DC2626") };
            Content.Children.Add(warn);
        }

        // Price cards
        var priceCard = new Border { Style = (Style)Resources["AppCard"] };
        var pg = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 12 };
        pg.Add(StatBox("Selling Price",  $"${p.SellingPrice:N2}",  "#2563EB"), 0, 0);
        pg.Add(StatBox("Purchase Price", $"${p.PurchasePrice:N2}", "#64748B"), 1, 0);
        pg.Add(StatBox("Margin",         $"{p.Margin}%",            "#059669"), 2, 0);
        priceCard.Content = pg;

        // Details
        var detail = new Border { Style = (Style)Resources["AppCard"] };
        var dv = new VerticalStackLayout { Spacing = 12 };
        dv.Children.Add(new Label { Text = p.Name, FontSize = 18, FontAttributes = FontAttributes.Bold });
        dv.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });
        void Row(string l, string v) => dv.Children.Add(DetailRow(l, v));
        Row("SKU",         p.SKU);
        Row("Barcode",     p.Barcode);
        Row("Category",    p.Category);
        Row("Brand",       p.Brand);
        Row("Unit",        p.Unit);
        Row("Tax",         $"{p.TaxPercent}%");
        Row("Stock",       $"{p.Stock} {p.Unit}");
        Row("Min. Stock",  $"{p.MinimumStock} {p.Unit}");
        Row("Status",      p.StatusLabel);
        if (!string.IsNullOrEmpty(p.Description)) Row("Description", p.Description);
        detail.Content = dv;

        Content.Children.Add(priceCard);
        Content.Children.Add(detail);
    }

    private static View StatBox(string label, string value, string hex)
    {
        var v = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center };
        v.Children.Add(new Label { Text = value, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(hex), HorizontalOptions = LayoutOptions.Center });
        v.Children.Add(new Label { Text = label, FontSize = 11, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center });
        return v;
    }

    private static View DetailRow(string l, string v)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new GridLength(140)), new ColumnDefinition(GridLength.Star)) };
        g.Add(new Label { Text = l, FontSize = 13, TextColor = Color.FromArgb("#64748B") }, 0, 0);
        g.Add(new Label { Text = v, FontSize = 13, FontAttributes = FontAttributes.Bold },  1, 0);
        return g;
    }

    private async void OnEditClicked(object s, EventArgs e) => await Shell.Current.GoToAsync($"{AppRoutes.ProductEdit}?id={_product?.LocalId}");
    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");
    private async void OnDeleteClicked(object s, EventArgs e)
    {
        if (_product == null) return;
        if (!await DialogHelper.ConfirmDeleteAsync("Product", $"Delete {_product.Name}?")) return;
        await _svc.DeleteAsync(_product.LocalId);
        await Shell.Current.GoToAsync("..");
    }
}
