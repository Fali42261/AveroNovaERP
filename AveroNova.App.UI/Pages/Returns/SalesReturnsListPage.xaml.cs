using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Returns;

public partial class SalesReturnsListPage : ContentPage
{
    private readonly IReturnService  _svc;
    private readonly ICompanyService _company;

    public SalesReturnsListPage(IReturnService svc, ICompanyService company)
    { InitializeComponent(); _svc = svc; _company = company; }

    protected override async void OnAppearing()    { base.OnAppearing(); await LoadAsync(); }
    private async void OnRefreshing(object s, EventArgs e) { await LoadAsync(); Refresher.IsRefreshing = false; }

    private async Task LoadAsync()
    {
        var items = await _svc.GetSalesReturnsAsync(_company.CurrentCompany?.LocalId ?? Guid.Empty);
        List.Children.Clear();
        foreach (var r in items) List.Children.Add(BuildRow(r));
        if (items.Count == 0) List.Children.Add(new Label { Text = "No sales returns.", FontSize = 14, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40) });
    }

    private View BuildRow(SalesReturnModel r)
    {
        var border = new Border { BackgroundColor = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E293B") : Colors.White, Stroke = Color.FromArgb("#E2E8F0"), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }, Padding = new Thickness(14, 12) };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };

        var left = new VerticalStackLayout { Spacing = 3 };
        left.Children.Add(new Label { Text = r.ReturnNumber, FontSize = 14, FontAttributes = FontAttributes.Bold });
        left.Children.Add(new Label { Text = r.CustomerName, FontSize = 13, TextColor = Color.FromArgb("#64748B") });
        left.Children.Add(new Label { Text = $"Invoice: {r.InvoiceNumber}  •  {r.ReturnDate:dd MMM yyyy}", FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });

        var right = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = $"${r.RefundAmount:N2}", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#DC2626") });
        right.Children.Add(new Label { Text = r.StatusLabel, FontSize = 11, TextColor = Color.FromArgb("#64748B") });

        grid.Add(left,  0, 0);
        grid.Add(right, 1, 0);
        border.Content = grid;
        return border;
    }

    private async void OnNewClicked(object s, EventArgs e) => await Shell.Current.GoToAsync(AppRoutes.SalesReturnNew);
}
