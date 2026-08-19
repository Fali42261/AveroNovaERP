using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Company;

public partial class CompanyListPage : ContentPage
{
    private readonly ICompanyService _svc;
    private List<CompanyModel> _items = [];

    public CompanyListPage(ICompanyService svc) { InitializeComponent(); _svc = svc; }

    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async void OnRefreshing(object s, EventArgs e) { await LoadAsync(); Refresher.IsRefreshing = false; }

    private async Task LoadAsync()
    {
        _items = await _svc.GetAllAsync();
        LblCount.Text = $"{_items.Count} compan{(_items.Count == 1 ? "y" : "ies")}";
        CompanyCards.Children.Clear();
        foreach (var c in _items) CompanyCards.Children.Add(BuildCard(c));
        if (_items.Count == 0) CompanyCards.Children.Add(new Label { Text = "No companies found. Add your first company.", FontSize = 14, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40) });
    }

    private View BuildCard(CompanyModel c)
    {
        var border = new Border
        {
            Style = (Style)Resources["ListRow"],
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) }
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 14 };

        // Avatar
        var avatar = new Border { WidthRequest = 46, HeightRequest = 46, BackgroundColor = Color.FromArgb("#EFF6FF"), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }, VerticalOptions = LayoutOptions.Center };
        avatar.Content = new Label { Text = c.Initials, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#2563EB"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

        // Info
        var info = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(new Label { Text = c.Name, FontSize = 14, FontAttributes = FontAttributes.Bold });
        info.Children.Add(new Label { Text = c.Email, FontSize = 12, TextColor = Color.FromArgb("#64748B") });
        info.Children.Add(new Label { Text = $"{c.City}, {c.Country}  •  {c.Currency}", FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });

        // Actions
        var actions = new VerticalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End };
        if (c.IsCurrentCompany)
        {
            var badge = new Border { BackgroundColor = Color.FromArgb("#EFF6FF"), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) }, Padding = new Thickness(8, 3) };
            badge.Content = new Label { Text = "Current", FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#2563EB") };
            actions.Children.Add(badge);
        }
        else
        {
            var switchBtn = new Button { Text = "Switch", Style = (Style)Resources["SmallSecondaryButton"] };
            switchBtn.Clicked += async (_, _) =>
            {
                var (ok, error) = await _svc.SwitchCompanyAsync(c.LocalId);
                if (!ok)
                {
                    await DisplayAlertAsync("Subscription", error ?? "Unable to switch company.", "OK");
                    return;
                }

                await LoadAsync();
            };
            actions.Children.Add(switchBtn);
        }

        var editBtn = new Button { Text = "Edit", Style = (Style)Resources["SmallButton"] };
        editBtn.Clicked += async (_, _) => await Shell.Current.GoToAsync($"{AppRoutes.CompanyEdit}?id={c.LocalId}");
        actions.Children.Add(editBtn);

        grid.Add(avatar, 0, 0);
        grid.Add(info,   1, 0);
        grid.Add(actions, 2, 0);
        border.Content = grid;
        return border;
    }

    private async void OnAddClicked(object s, EventArgs e) => await Shell.Current.GoToAsync(AppRoutes.CompanyAdd);
}
