using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Products;

[QueryProperty(nameof(ProductId), "id")]
public partial class ProductViewPage : ContentPage, IHostedPage
{
    private readonly IProductService _svc;
    private readonly IMainContentNavigator _navigator;
    private readonly IServiceProvider _services;
    private readonly ISettingsService _settings;
    private ProductModel? _product;
    private string _currencySymbol = "₹";
    public string? ProductId { get; set; }

    public ProductViewPage(
        IProductService svc,
        IMainContentNavigator navigator,
        IServiceProvider services,
        ISettingsService settings)
    {
        InitializeComponent();
        _svc = svc;
        _navigator = navigator;
        _services = services;
        _settings = settings;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }

    public async Task LoadForHostAsync()
    {
        try
        {
            var appSettings = await _settings.GetAsync();
            _currencySymbol = string.IsNullOrWhiteSpace(appSettings.CurrencySymbol)
                ? (appSettings.Currency == "INR" ? "₹" : "$")
                : appSettings.CurrencySymbol;

            if (!string.IsNullOrEmpty(ProductId) && Guid.TryParse(ProductId, out var id))
            {
                _product = await _svc.GetByIdAsync(id);
                if (_product != null) BuildContent(_product);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProductView] Load failed: {ex}");
            Content.Children.Clear();
            Content.Children.Add(new Label
            {
                Text = "Product details could not be loaded.",
                TextColor = Color.FromArgb("#DC2626"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 32)
            });
        }
    }

    private void BuildContent(ProductModel p)
    {
        Content.Children.Clear();

        if (p.IsLowStock)
        {
            Content.Children.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#FEF2F2"),
                Stroke = Color.FromArgb("#FECACA"),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
                Padding = new Thickness(14, 10),
                Content = new Label
                {
                    Text = $"⚠ Low Stock Warning: Only {p.Stock} units remaining. Minimum: {p.MinimumStock}",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#DC2626")
                }
            });
        }

        var priceCard = new Border
        {
            Stroke = Color.FromArgb("#E2E8F0"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Padding = new Thickness(16)
        };
        var pg = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)),
            ColumnSpacing = 12
        };
        pg.Add(StatBox("Selling Price", $"{_currencySymbol}{p.SellingPrice:N2}", "#2563EB"), 0, 0);
        pg.Add(StatBox("Purchase Price", $"{_currencySymbol}{p.PurchasePrice:N2}", "#64748B"), 1, 0);
        pg.Add(StatBox("Margin", $"{p.Margin}%", "#059669"), 2, 0);
        priceCard.Content = pg;

        var detail = new Border
        {
            Stroke = Color.FromArgb("#E2E8F0"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Padding = new Thickness(16)
        };
        var dv = new VerticalStackLayout { Spacing = 12 };
        dv.Children.Add(new Label { Text = p.Name, FontSize = 18, FontAttributes = FontAttributes.Bold });
        dv.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#E2E8F0") });
        void Row(string l, string v) => dv.Children.Add(DetailRow(l, v));
        Row("SKU", p.SKU);
        Row("Barcode", p.Barcode);
        Row("Category", p.Category);
        Row("Brand", p.Brand);
        Row("Unit", p.Unit);
        Row("Tax", $"{p.TaxPercent}%");
        Row("Stock", $"{p.Stock} {p.Unit}");
        Row("Min. Stock", $"{p.MinimumStock} {p.Unit}");
        Row("Status", p.StatusLabel);
        if (!string.IsNullOrEmpty(p.Description)) Row("Description", p.Description);
        detail.Content = dv;

        Content.Children.Add(priceCard);
        Content.Children.Add(detail);
    }

    private static View StatBox(string label, string value, string hex)
    {
        var stack = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center };
        stack.Children.Add(new Label
        {
            Text = value,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb(hex),
            HorizontalOptions = LayoutOptions.Center
        });
        stack.Children.Add(new Label
        {
            Text = label,
            FontSize = 11,
            TextColor = Color.FromArgb("#64748B"),
            HorizontalOptions = LayoutOptions.Center
        });
        return stack;
    }

    private static View DetailRow(string label, string value)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(new GridLength(140)),
                new ColumnDefinition(GridLength.Star))
        };
        grid.Add(new Label { Text = label, FontSize = 13, TextColor = Color.FromArgb("#64748B") }, 0, 0);
        grid.Add(new Label { Text = value, FontSize = 13, FontAttributes = FontAttributes.Bold }, 1, 0);
        return grid;
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_product is null) return;
        try
        {
            var page = ActivatorUtilities.CreateInstance<ProductFormPage>(_services);
            page.EditId = _product.LocalId.ToString("D");
            await _navigator.NavigateAsync(page, "Edit Product", "Home / Products / Edit");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProductView] Edit failed: {ex}");
            await DisplayAlert("Product", "Product could not be opened for editing.", "OK");
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        try { await _navigator.GoBackAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ProductView] Back failed: {ex}"); }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_product == null) return;
        try
        {
            if (!await DialogHelper.ConfirmDeleteAsync("Product", $"Delete {_product.Name}?")) return;
            var result = await _svc.DeleteAsync(_product.LocalId);
            if (!result.Ok)
            {
                await DisplayAlert("Product", result.Error ?? "Delete failed.", "OK");
                return;
            }
            await _navigator.GoBackAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProductView] Delete failed: {ex}");
            await DisplayAlert("Product", "Product could not be deleted.", "OK");
        }
    }
}
