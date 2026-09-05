using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Inventory;

public partial class StockMovementPage : ContentPage, IHostedPage
{
    private readonly IInventoryService _svc;
    private readonly ICompanyService   _company;
    private readonly IMainContentNavigator _navigator;

    public StockMovementPage(IInventoryService svc, ICompanyService company, IMainContentNavigator navigator)
    { InitializeComponent(); _svc = svc; _company = company; _navigator = navigator; }

    protected override async void OnAppearing()    { base.OnAppearing(); await LoadAsync(); }
    public Task LoadForHostAsync() => LoadAsync();
    private async void OnRefreshing(object s, EventArgs e) { await LoadAsync(); Refresher.IsRefreshing = false; }

    private async Task LoadAsync()
    {
        var cid   = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        var items = await _svc.GetMovementsAsync(cid);
        MovementList.Children.Clear();
        if (items.Count == 0) { MovementList.Children.Add(new Label { Text = "No stock movements recorded.", FontSize = 14, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40) }); return; }
        foreach (var m in items) MovementList.Children.Add(BuildRow(m));
    }

    private static View BuildRow(StockMovementModel m)
    {
        var (typeBg, typeColor) = m.Type switch
        {
            StockMovementType.In         => ("#ECFDF5", "#059669"),
            StockMovementType.Out        => ("#FEF2F2", "#DC2626"),
            StockMovementType.Adjustment => ("#FFFBEB", "#D97706"),
            _                            => ("#EFF6FF", "#2563EB")
        };

        var border = new Border { BackgroundColor = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E293B") : Colors.White, Stroke = Color.FromArgb("#E2E8F0"), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }, Padding = new Thickness(14, 12) };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };

        var left = new VerticalStackLayout { Spacing = 3 };
        left.Children.Add(new Label { Text = m.ProductName, FontSize = 14, FontAttributes = FontAttributes.Bold });
        left.Children.Add(new Label { Text = $"SKU: {m.SKU}  •  Ref: {m.Reference}", FontSize = 12, TextColor = Color.FromArgb("#64748B") });
        left.Children.Add(new Label { Text = m.Notes, FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });

        var right = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.End };
        var typeBadge = new Border { BackgroundColor = Color.FromArgb(typeBg), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) }, Padding = new Thickness(8, 3) };
        typeBadge.Content = new Label { Text = m.TypeLabel, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(typeColor) };
        right.Children.Add(typeBadge);
        var qty = m.Quantity >= 0 ? $"+{m.Quantity}" : m.Quantity.ToString();
        right.Children.Add(new Label { Text = qty, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(typeColor), HorizontalOptions = LayoutOptions.End });
        right.Children.Add(new Label { Text = $"{m.StockBefore} → {m.StockAfter}", FontSize = 11, TextColor = Color.FromArgb("#94A3B8"), HorizontalOptions = LayoutOptions.End });

        grid.Add(left,  0, 0);
        grid.Add(right, 1, 0);
        border.Content = grid;
        return border;
    }

    private async void OnBackClicked(object s, EventArgs e) => await _navigator.GoBackAsync();
}
