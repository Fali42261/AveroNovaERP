using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Customers;

public partial class CustomersListPage : ContentPage
{
    private readonly ICustomerService _svc;
    private readonly ICompanyService  _company;
    private List<CustomerModel>       _all  = [];
    private List<CustomerModel>       _shown = [];

    public CustomersListPage(ICustomerService svc, ICompanyService company)
    { InitializeComponent(); _svc = svc; _company = company; }

    protected override async void OnAppearing()    { base.OnAppearing(); await LoadAsync(); }
    private async void OnRefreshing(object s, EventArgs e) { await LoadAsync(); Refresher.IsRefreshing = false; }
    private async void OnRefreshClicked(object s, EventArgs e) { await LoadAsync(); }

    private async Task LoadAsync()
    {
        var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        _all    = await _svc.GetAllAsync(cid);
        RenderList(_all);
    }

    private async void OnSearchChanged(object s, TextChangedEventArgs e)
    {
        var q = e.NewTextValue?.Trim() ?? "";
        if (string.IsNullOrEmpty(q)) { RenderList(_all); return; }
        var cid    = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        _shown     = await _svc.SearchAsync(cid, q);
        RenderList(_shown);
    }

    private void RenderList(List<CustomerModel> items)
    {
        LblCount.Text = $"{items.Count} customer{(items.Count == 1 ? "" : "s")}";
        CustomerList.Children.Clear();
        if (items.Count == 0) { CustomerList.Children.Add(new Label { Text = "No customers found.", FontSize = 14, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40) }); return; }
        foreach (var c in items) CustomerList.Children.Add(BuildRow(c));
    }

    private View BuildRow(CustomerModel c)
    {
        var border = new Border
        {
            BackgroundColor = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E293B") : Colors.White,
            Stroke          = Color.FromArgb("#E2E8F0"),
            StrokeThickness = 1,
            StrokeShape     = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Padding         = new Thickness(14, 12)
        };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };

        // Avatar
        var av = new Border { WidthRequest = 42, HeightRequest = 42, BackgroundColor = Color.FromArgb("#EFF6FF"), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(21) }, VerticalOptions = LayoutOptions.Center };
        av.Content = new Label { Text = c.Initials, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#2563EB"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

        // Info
        var info = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(new Label { Text = c.Name, FontSize = 14, FontAttributes = FontAttributes.Bold });
        info.Children.Add(new Label { Text = c.Email, FontSize = 12, TextColor = Color.FromArgb("#64748B") });
        info.Children.Add(new Label { Text = c.Phone, FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });

        // Right
        var right = new VerticalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End };
        var statusBadge = new Border
        {
            BackgroundColor = c.Status == CustomerStatus.Active ? Color.FromArgb("#ECFDF5") : Color.FromArgb("#FEF2F2"),
            StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            Padding = new Thickness(7, 2)
        };
        statusBadge.Content = new Label { Text = c.StatusLabel, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = c.Status == CustomerStatus.Active ? Color.FromArgb("#059669") : Color.FromArgb("#DC2626") };
        right.Children.Add(statusBadge);
        if (c.OutstandingBalance > 0) right.Children.Add(new Label { Text = $"${c.OutstandingBalance:N0} due", FontSize = 11, TextColor = Color.FromArgb("#D97706"), HorizontalOptions = LayoutOptions.End });

        var actionsRow = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.End };
        var viewBtn = new Button { Text = "View", Style = (Style)Resources["SmallSecondaryButton"] };
        viewBtn.Clicked += async (_, _) => await Shell.Current.GoToAsync($"{AppRoutes.CustomerView}?id={c.LocalId}");
        var editBtn = new Button { Text = "Edit", Style = (Style)Resources["SmallButton"] };
        editBtn.Clicked += async (_, _) => await Shell.Current.GoToAsync($"{AppRoutes.CustomerEdit}?id={c.LocalId}");
        actionsRow.Children.Add(viewBtn);
        actionsRow.Children.Add(editBtn);
        right.Children.Add(actionsRow);

        grid.Add(av,    0, 0);
        grid.Add(info,  1, 0);
        grid.Add(right, 2, 0);
        border.Content = grid;
        return border;
    }

    private async void OnAddClicked(object s, EventArgs e)    => await Shell.Current.GoToAsync(AppRoutes.CustomerAdd);
    private void OnFilterClicked(object s, EventArgs e) => DisplayAlert("Filter", "Filter options coming soon.", "OK");
}
