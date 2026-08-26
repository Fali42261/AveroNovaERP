using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Payments;

public partial class PaymentsListPage : ContentPage
{
    private readonly IPaymentService _svc;
    private readonly ICompanyService _company;

    public PaymentsListPage(IPaymentService svc, ICompanyService company)
    { InitializeComponent(); _svc = svc; _company = company; }

    public Task ReloadAsync() => LoadAsync();
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async void OnRefreshing(object s, EventArgs e) { await LoadAsync(); Refresher.IsRefreshing = false; }

    private async Task LoadAsync()
    {
        var items = await _svc.GetAllAsync(_company.CurrentCompany?.LocalId ?? Guid.Empty);
        LblCount.Text = $"{items.Count} payment{(items.Count == 1 ? "" : "s")}";
        List.Children.Clear();
        foreach (var p in items.OrderByDescending(i => i.PaymentDate)) List.Children.Add(BuildRow(p));
        if (items.Count == 0) List.Children.Add(new Label { Text = "No payments found.", FontSize = 14, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40) });
    }

    private View BuildRow(PaymentModel p)
    {
        var border = new Border { BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E293B") : Colors.White, Stroke = Color.FromArgb("#E2E8F0"), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }, Padding = new Thickness(14, 12) };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };
        var left = new VerticalStackLayout { Spacing = 4 };
        left.Children.Add(new Label { Text = p.PaymentNumber, FontSize = 14, FontAttributes = FontAttributes.Bold });
        left.Children.Add(new Label { Text = p.PartyName, FontSize = 13, TextColor = Color.FromArgb("#64748B") });
        left.Children.Add(new Label { Text = $"{p.PaymentDate:dd MMM yyyy}  •  {p.MethodLabel}", FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });
        var right = new VerticalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = $"₹{p.Amount:N2}", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#059669"), HorizontalOptions = LayoutOptions.End });
        var viewBtn = new Button { Text = "View", Style = TryStyle("SmallSecondaryButton"), HeightRequest = 36, Padding = new Thickness(14, 0) };
        viewBtn.Clicked += async (_, _) => await OpenViewAsync(p.LocalId); right.Children.Add(viewBtn);
        grid.Add(left, 0, 0); grid.Add(right, 1, 0); border.Content = grid; return border;
    }

    private async Task OpenFormAsync(Guid? id = null)
    {
        var page = new PaymentFormPage(_svc, _company) { CloseRequested = CloseActionOverlay };
        await page.LoadAsync(id); ShowActionPage(page);
    }

    private async Task OpenViewAsync(Guid id)
    {
        var page = new PaymentViewPage(_svc) { CloseRequested = CloseActionOverlay };
        await page.LoadAsync(id); ShowActionPage(page);
    }

    private void ShowActionPage(ContentPage page)
    {
        var content = page.Content; if (content == null) return; page.Content = null;
        ActionContent.Content = content; ActionOverlay.IsVisible = true;
    }

    private void CloseActionOverlay()
    {
        ActionContent.Content = null; ActionOverlay.IsVisible = false; _ = LoadAsync();
    }

    private async void OnAddClicked(object s, EventArgs e) => await OpenFormAsync();

    private static Style? TryStyle(string key)
        => Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Style style ? style : null;
}
