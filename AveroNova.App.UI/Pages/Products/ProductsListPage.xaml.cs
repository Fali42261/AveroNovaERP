using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Products;

public partial class ProductsListPage : ContentPage, IHostedPage
{
    private readonly IProductService _svc;
    private readonly ICompanyService _company;
    private readonly IMainContentNavigator _navigator;
    private readonly IServiceProvider _services;
    private readonly ISettingsService _settings;
    private List<ProductModel> _all = [];
    private string _currencySymbol = "₹";
    private bool _loading;

    public ProductsListPage(
        IProductService svc,
        ICompanyService company,
        IMainContentNavigator navigator,
        IServiceProvider services,
        ISettingsService settings)
    {
        InitializeComponent();
        _svc = svc;
        _company = company;
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
        if (_loading) return;
        _loading = true;
        try
        {
            var appSettings = await _settings.GetAsync();
            _currencySymbol = string.IsNullOrWhiteSpace(appSettings.CurrencySymbol)
                ? (appSettings.Currency == "INR" ? "₹" : "$")
                : appSettings.CurrencySymbol;

            var companyId = _company.CurrentCompany?.LocalId ?? Guid.Empty;
            _all = companyId == Guid.Empty ? [] : await _svc.GetAllAsync(companyId);
            RenderList(_all);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Products] Load failed: {ex}");
            RenderError("Products could not be loaded. Pull down to retry.");
        }
        finally
        {
            _loading = false;
        }
    }

    private async void OnRefreshing(object? s, EventArgs e)
    {
        try { await LoadForHostAsync(); }
        finally { Refresher.IsRefreshing = false; }
    }

    private async void OnSearchChanged(object? s, TextChangedEventArgs e)
    {
        try
        {
            var q = e.NewTextValue?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(q))
            {
                RenderList(_all);
                return;
            }

            var companyId = _company.CurrentCompany?.LocalId ?? Guid.Empty;
            var results = companyId == Guid.Empty ? [] : await _svc.SearchAsync(companyId, q);
            RenderList(results);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Products] Search failed: {ex}");
        }
    }

    private void RenderList(List<ProductModel> items)
    {
        LblCount.Text = $"{items.Count} product{(items.Count == 1 ? string.Empty : "s")}";
        ProductList.Children.Clear();
        if (items.Count == 0)
        {
            ProductList.Children.Add(new Label
            {
                Text = "No products found.",
                FontSize = 14,
                TextColor = Color.FromArgb("#64748B"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 40)
            });
            return;
        }

        foreach (var p in items.OrderBy(x => x.Name))
            ProductList.Children.Add(BuildRow(p));
    }

    private void RenderError(string message)
    {
        ProductList.Children.Clear();
        ProductList.Children.Add(new Label
        {
            Text = message,
            FontSize = 14,
            TextColor = Color.FromArgb("#DC2626"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 40)
        });
    }

    private View BuildRow(ProductModel p)
    {
        var border = new Border
        {
            BackgroundColor = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#1E293B") : Colors.White,
            Stroke = Color.FromArgb("#E2E8F0"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Padding = new Thickness(14, 12)
        };
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 12
        };

        var icon = new Border
        {
            WidthRequest = 44,
            HeightRequest = 44,
            BackgroundColor = Color.FromArgb("#FFFBEB"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = "📦",
                FontSize = 20,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };

        var info = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(new Label { Text = p.Name, FontSize = 14, FontAttributes = FontAttributes.Bold });
        info.Children.Add(new Label
        {
            Text = $"SKU: {p.SKU}  •  {p.Category}",
            FontSize = 12,
            TextColor = Color.FromArgb("#64748B")
        });
        if (p.IsLowStock)
        {
            info.Children.Add(new Label
            {
                Text = $"Low Stock: {p.Stock}",
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#DC2626")
            });
        }

        var right = new VerticalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label
        {
            Text = $"{_currencySymbol}{p.SellingPrice:N2}",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.End
        });
        right.Children.Add(new Label
        {
            Text = $"Stock: {p.Stock}",
            FontSize = 11,
            TextColor = Color.FromArgb("#64748B"),
            HorizontalOptions = LayoutOptions.End
        });

        var actRow = new HorizontalStackLayout { Spacing = 6 };
        var viewBtn = MakeActionButton("View", false);
        viewBtn.Clicked += async (_, _) =>
        {
            try
            {
                var page = ActivatorUtilities.CreateInstance<ProductViewPage>(_services);
                page.ProductId = p.LocalId.ToString("D");
                await _navigator.NavigateAsync(page, "Product Details", "Home / Products / Details");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Products] View failed: {ex}");
                await DisplayAlert("Products", "Product details could not be opened.", "OK");
            }
        };

        var editBtn = MakeActionButton("Edit", true);
        editBtn.Clicked += async (_, _) =>
        {
            try
            {
                var page = ActivatorUtilities.CreateInstance<ProductFormPage>(_services);
                page.EditId = p.LocalId.ToString("D");
                await _navigator.NavigateAsync(page, "Edit Product", "Home / Products / Edit");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Products] Edit failed: {ex}");
                await DisplayAlert("Products", "Product could not be opened for editing.", "OK");
            }
        };
        actRow.Children.Add(viewBtn);
        actRow.Children.Add(editBtn);
        right.Children.Add(actRow);

        grid.Add(icon, 0, 0);
        grid.Add(info, 1, 0);
        grid.Add(right, 2, 0);
        border.Content = grid;
        return border;
    }

    private static Button MakeActionButton(string text, bool primary) => new()
    {
        Text = text,
        FontSize = 12,
        HeightRequest = 36,
        Padding = new Thickness(12, 0),
        CornerRadius = 8,
        BackgroundColor = primary ? Color.FromArgb("#2563EB") : Colors.Transparent,
        TextColor = primary ? Colors.White : Color.FromArgb("#2563EB"),
        BorderColor = Color.FromArgb("#2563EB"),
        BorderWidth = 1
    };

    private async void OnAddClicked(object? s, EventArgs e)
    {
        try
        {
            var page = ActivatorUtilities.CreateInstance<ProductFormPage>(_services);
            await _navigator.NavigateAsync(page, "Add Product", "Home / Products / Add");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Products] Add failed: {ex}");
            await DisplayAlert("Products", "Product form could not be opened.", "OK");
        }
    }
}
