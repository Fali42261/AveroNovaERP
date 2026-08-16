using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Products;

public partial class ProductsListPage : ContentPage
{
    private readonly IProductService _svc;
    private readonly ICompanyService _company;
    private List<ProductModel> _all = [];

    public ProductsListPage(IProductService svc, ICompanyService company) { InitializeComponent(); _svc = svc; _company = company; }

    protected override async void OnAppearing()    { base.OnAppearing(); await LoadAsync(); }
    private async void OnRefreshing(object s, EventArgs e) { await LoadAsync(); Refresher.IsRefreshing = false; }

    private async Task LoadAsync()
    {
        _all = await _svc.GetAllAsync(_company.CurrentCompany?.LocalId ?? Guid.Empty);
        RenderList(_all);
    }

    private async void OnSearchChanged(object s, TextChangedEventArgs e)
    {
        var q = e.NewTextValue?.Trim() ?? "";
        if (string.IsNullOrEmpty(q)) { RenderList(_all); return; }
        var results = await _svc.SearchAsync(_company.CurrentCompany?.LocalId ?? Guid.Empty, q);
        RenderList(results);
    }

    private void RenderList(List<ProductModel> items)
    {
        LblCount.Text = $"{items.Count} product{(items.Count == 1 ? "" : "s")}";
        ProductList.Children.Clear();
        if (items.Count == 0) { ProductList.Children.Add(new Label { Text = "No products found.", FontSize = 14, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40) }); return; }
        foreach (var p in items) ProductList.Children.Add(BuildRow(p));
    }

    private View BuildRow(ProductModel p)
    {
        var border = new Border { BackgroundColor = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E293B") : Colors.White, Stroke = Color.FromArgb("#E2E8F0"), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }, Padding = new Thickness(14, 12) };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };

        var icon = new Border { WidthRequest = 44, HeightRequest = 44, BackgroundColor = Color.FromArgb("#FFFBEB"), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) }, VerticalOptions = LayoutOptions.Center };
        icon.Content = new Label { Text = "&#x1F4E6;", FontSize = 20, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

        var info = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(new Label { Text = p.Name, FontSize = 14, FontAttributes = FontAttributes.Bold });
        info.Children.Add(new Label { Text = $"SKU: {p.SKU}  •  {p.Category}", FontSize = 12, TextColor = Color.FromArgb("#64748B") });
        if (p.IsLowStock)
        {
            var warn = new Border { BackgroundColor = Color.FromArgb("#FEF2F2"), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) }, Padding = new Thickness(6, 2) };
            warn.Content = new Label { Text = $"Low Stock: {p.Stock}", FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#DC2626") };
            info.Children.Add(warn);
        }

        var right = new VerticalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = $"${p.SellingPrice:N2}", FontSize = 14, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.End });
        right.Children.Add(new Label { Text = $"Stock: {p.Stock}", FontSize = 11, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.End });
        var actRow = new HorizontalStackLayout { Spacing = 6 };
        var viewBtn = new Button { Text = "View", Style = (Style)Resources["SmallSecondaryButton"] };
        viewBtn.Clicked += async (_, _) => await Shell.Current.GoToAsync($"{AppRoutes.ProductView}?id={p.LocalId}");
        var editBtn = new Button { Text = "Edit", Style = (Style)Resources["SmallButton"] };
        editBtn.Clicked += async (_, _) => await Shell.Current.GoToAsync($"{AppRoutes.ProductEdit}?id={p.LocalId}");
        actRow.Children.Add(viewBtn); actRow.Children.Add(editBtn);
        right.Children.Add(actRow);

        grid.Add(icon,  0, 0);
        grid.Add(info,  1, 0);
        grid.Add(right, 2, 0);
        border.Content = grid;
        return border;
    }

    private async void OnAddClicked(object s, EventArgs e) => await Shell.Current.GoToAsync(AppRoutes.ProductAdd);
}
