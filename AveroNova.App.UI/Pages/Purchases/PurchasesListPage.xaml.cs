using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Purchases;

public partial class PurchasesListPage : ContentPage
{
    private readonly IPurchaseService _svc;
    private readonly ICompanyService _company;
    private readonly IProductService _products;

    public PurchasesListPage(IPurchaseService svc, ICompanyService company, IProductService products)
    { InitializeComponent(); _svc = svc; _company = company; _products = products; }

    public Task ReloadAsync() => LoadAsync();
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async void OnRefreshing(object? s, EventArgs e) { await LoadAsync(); Refresher.IsRefreshing = false; }

    private async Task LoadAsync()
    {
        var items = await _svc.GetAllAsync(_company.CurrentCompany?.LocalId ?? Guid.Empty);
        LblCount.Text = $"{items.Count} purchase{(items.Count == 1 ? "" : "s")}";
        List.Children.Clear();
        foreach (var p in items.OrderByDescending(i => i.PurchaseDate)) List.Children.Add(BuildRow(p));
        if (items.Count == 0) List.Children.Add(new Label { Text = "No purchases found.", FontSize = 14, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40) });
    }

    private View BuildRow(PurchaseModel p)
    {
        var (bg, color) = p.Status switch
        {
            PurchaseStatus.Received => ("#ECFDF5", "#059669"), PurchaseStatus.Ordered => ("#EFF6FF", "#2563EB"),
            PurchaseStatus.PartialReceived => ("#FFFBEB", "#D97706"), PurchaseStatus.Cancelled => ("#F3F4F6", "#9CA3AF"), _ => ("#F9FAFB", "#6B7280")
        };
        var border = new Border { BackgroundColor = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E293B") : Colors.White, Stroke = Color.FromArgb("#E2E8F0"), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }, Padding = new Thickness(14, 12) };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };
        var left = new VerticalStackLayout { Spacing = 4 };
        left.Children.Add(new Label { Text = p.PurchaseNumber, FontSize = 14, FontAttributes = FontAttributes.Bold });
        left.Children.Add(new Label { Text = p.SupplierName, FontSize = 13, TextColor = Color.FromArgb("#64748B") });
        left.Children.Add(new Label { Text = p.PurchaseDate.ToString("dd MMM yyyy"), FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });
        var right = new VerticalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = $"₹{p.GrandTotal:N2}", FontSize = 15, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.End });
        var badge = new Border { BackgroundColor = Color.FromArgb(bg), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) }, Padding = new Thickness(8, 3), Content = new Label { Text = p.StatusLabel, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(color) } };
        right.Children.Add(badge);
        var viewBtn = new Button { Text = "View", Style = TryStyle("SmallSecondaryButton"), HeightRequest = 36, Padding = new Thickness(14, 0) };
        viewBtn.Clicked += async (_, _) => await OpenViewAsync(p.LocalId); right.Children.Add(viewBtn);
        grid.Add(left, 0, 0); grid.Add(right, 1, 0); border.Content = grid; return border;
    }

    private async Task OpenFormAsync(Guid? id = null)
    {
        var page = new PurchaseFormPage(_svc, _company, _products) { CloseRequested = CloseActionOverlay };
        await page.LoadAsync(id); ShowActionPage(page);
    }

    private async Task OpenViewAsync(Guid id)
    {
        var page = new PurchaseViewPage(_svc) { CloseRequested = CloseActionOverlay, EditRequested = editId => OpenFormAsync(editId) };
        await page.LoadAsync(id); ShowActionPage(page);
    }

    private void ShowActionPage(ContentPage page)
    {
        var content = page.Content; if (content == null) return; page.Content = null; ActionContent.Content = content; ActionOverlay.IsVisible = true;
    }

    private void CloseActionOverlay() { ActionContent.Content = null; ActionOverlay.IsVisible = false; _ = LoadAsync(); }
    private async void OnNewClicked(object? s, EventArgs e) => await OpenFormAsync();
    private static Style? TryStyle(string key) => Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Style style ? style : null;
}
