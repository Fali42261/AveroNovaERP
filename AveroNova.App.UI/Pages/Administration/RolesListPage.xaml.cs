using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Administration;

public partial class RolesListPage : ContentPage
{
    private readonly IUserService _svc;
    private readonly ICompanyService _company;
    private List<RoleModel> _all = [];

    public RolesListPage(
    IUserService svc,
    ICompanyService company)
    {
        InitializeComponent();

        _svc = svc;
        _company = company;
    }

    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async void OnRefreshing(object s, EventArgs e) { await LoadAsync(); Refresher.IsRefreshing = false; }
    private async void OnRefreshClicked(object s, EventArgs e) { await LoadAsync(); }

    private async Task LoadAsync()
    {
        //_all = await _svc.GetAllRolesAsync();
        var company = _company.CurrentCompany;

        if (company == null)
        {
            _all = [];
            RenderList(_all);
            return;
        }

        _all = await _svc.GetRolesAsync(company.LocalId);
        RenderList(_all);
    }

    public Task ReloadAsync() => LoadAsync();

    private void OnSearchChanged(object s, TextChangedEventArgs e)
    {
        var q = e.NewTextValue?.Trim() ?? "";
        if (string.IsNullOrEmpty(q)) { RenderList(_all); return; }
        var shown = _all.Where(r => r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || (r.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        RenderList(shown);
    }

    private void RenderList(List<RoleModel> items)
    {
        LblCount.Text = $"{items.Count} role{(items.Count == 1 ? "" : "s")}";
        RoleList.Children.Clear();
        if (items.Count == 0) { RoleList.Children.Add(new Label { Text = "No roles found.", FontSize = 14, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40) }); return; }
        foreach (var r in items) RoleList.Children.Add(BuildRow(r));
    }

    private View BuildRow(RoleModel r)
    {
        var border = new Border
        {
            BackgroundColor = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E293B") : Colors.White,
            Stroke = Color.FromArgb("#E2E8F0"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Padding = new Thickness(14, 12)
        };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };

        var iconBorder = new Border { WidthRequest = 42, HeightRequest = 42, BackgroundColor = Color.FromArgb("#F3E8FF"), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(21) }, VerticalOptions = LayoutOptions.Center };
        iconBorder.Content = new Label { Text = (string)Microsoft.Maui.Controls.Application.Current?.Resources["IconRoles"] ?? "\uD83D\uDEE1", FontSize = 18, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

        var info = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(new Label { Text = r.Name, FontSize = 14, FontAttributes = FontAttributes.Bold });
        info.Children.Add(new Label { Text = r.Description ?? "No description", FontSize = 12, TextColor = Color.FromArgb("#64748B") });
        info.Children.Add(new Label { Text = $"{r.UserCount} user{(r.UserCount == 1 ? "" : "s")}", FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });

        var actionsRow = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End };
        var editBtn = new Button { Text = "Edit", Style = (Style)Resources["SmallButton"] };
        editBtn.Clicked += async (_, _) => await Shell.Current.GoToAsync($"{AppRoutes.RoleEdit}?id={r.LocalId}");
        actionsRow.Children.Add(editBtn);

        grid.Add(iconBorder, 0, 0);
        grid.Add(info, 1, 0);
        grid.Add(actionsRow, 2, 0);
        border.Content = grid;
        return border;
    }

    private async void OnAddClicked(object s, EventArgs e) => await Shell.Current.GoToAsync(AppRoutes.RoleAdd);
}
