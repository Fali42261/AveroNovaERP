using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Inventory;

public partial class InventoryPage : ContentPage
{
    private readonly IInventoryService _svc;
    private readonly IProductService _product;
    private readonly ICompanyService _company;

    public InventoryPage(IInventoryService svc, IProductService product, ICompanyService company)
    { InitializeComponent(); _svc = svc; _product = product; _company = company; }

    public Task ReloadAsync() => LoadAsync();
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async void OnRefreshing(object s, EventArgs e) { await LoadAsync(); Refresher.IsRefreshing = false; }

    private async Task LoadAsync()
    {
        var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        var items = await _svc.GetInventoryAsync(cid);
        LblCount.Text = $"{items.Count} item{(items.Count == 1 ? "" : "s")}";
        LblTotalItems.Text = items.Count.ToString();
        LblLowStock.Text = items.Count(i => i.IsLowStock).ToString();
        LblInStock.Text = items.Count(i => !i.IsLowStock && i.CurrentStock > 0).ToString();
        LblOutOfStock.Text = items.Count(i => i.CurrentStock == 0).ToString();
        InventoryList.Children.Clear();
        foreach (var item in items) InventoryList.Children.Add(BuildRow(item));
        if (items.Count == 0)
            InventoryList.Children.Add(new Label { Text = "No inventory items.", FontSize = 14, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40) });
    }

    private View BuildRow(InventoryItemModel item)
    {
        var border = new Border
        {
            BackgroundColor = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E293B") : Colors.White,
            Stroke = item.IsLowStock ? Color.FromArgb("#FECACA") : Color.FromArgb("#E2E8F0"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Padding = new Thickness(14, 12)
        };
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 12
        };
        var info = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(new Label { Text = item.ProductName, FontSize = 14, FontAttributes = FontAttributes.Bold });
        info.Children.Add(new Label { Text = $"SKU: {item.SKU}  •  {item.Category}", FontSize = 12, TextColor = Color.FromArgb("#64748B") });
        info.Children.Add(new Label { Text = $"Updated: {item.LastUpdated:dd MMM yyyy}", FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });
        var stockInfo = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End };
        stockInfo.Children.Add(new Label { Text = item.CurrentStock.ToString(), FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = item.IsLowStock ? Color.FromArgb("#DC2626") : Color.FromArgb("#059669"), HorizontalOptions = LayoutOptions.End });
        stockInfo.Children.Add(new Label { Text = $"Min: {item.MinimumStock}", FontSize = 11, TextColor = Color.FromArgb("#94A3B8"), HorizontalOptions = LayoutOptions.End });
        var adjBtn = new Button { Text = "Adjust", Style = TryStyle("SmallButton") };
        adjBtn.Clicked += async (_, _) => await OpenStockAdjustAsync(item.ProductId);
        grid.Add(info, 0, 0); grid.Add(stockInfo, 1, 0); grid.Add(adjBtn, 2, 0);
        border.Content = grid;
        return border;
    }

    private Task OpenStockAdjustAsync(Guid? productId = null)
    {
        var page = new StockAdjustPage(_svc, _product, _company) { ProductIdParam = productId?.ToString() };
        page.CloseRequested = CloseActionOverlay;
        return ShowActionPageAsync(page);
    }

    private Task OpenStockHistoryAsync()
    {
        var page = new StockMovementPage(_svc, _company);
        page.CloseRequested = CloseActionOverlay;
        return ShowActionPageAsync(page);
    }

    private Task ShowActionPageAsync(ContentPage page)
    {
        if (page.Content == null) return Task.CompletedTask;
        ActionContent.Content = page.Content;
        ActionOverlay.IsVisible = true;
        return Task.CompletedTask;
    }

    private void CloseActionOverlay()
    {
        ActionContent.Content = null;
        ActionOverlay.IsVisible = false;
        _ = LoadAsync();
    }

    private async void OnAdjustClicked(object s, EventArgs e) => await OpenStockAdjustAsync();
    private async void OnHistoryClicked(object s, EventArgs e) => await OpenStockHistoryAsync();

    private static Style? TryStyle(string key)
        => Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Style style ? style : null;
}
