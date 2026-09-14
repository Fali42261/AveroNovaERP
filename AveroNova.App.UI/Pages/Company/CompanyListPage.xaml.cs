using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Company;

public partial class CompanyListPage : ContentPage, IHostedPage
{
    private const int FreePlanTotalCompanyLimit = 2; // registration company + 1 extra company
    private readonly ICompanyService _svc;
    private readonly IMainContentNavigator _navigator;
    private readonly IServiceProvider _services;
    private List<CompanyModel> _items = [];
    private bool _loading;

    public CompanyListPage(ICompanyService svc, IMainContentNavigator navigator, IServiceProvider services)
    {
        InitializeComponent();
        _svc = svc;
        _navigator = navigator;
        _services = services;
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
            _items = await _svc.GetAllAsync();
            LblCount.Text = $"{_items.Count} compan{(_items.Count == 1 ? "y" : "ies")}";

            var canAdd = _items.Count < FreePlanTotalCompanyLimit;
            BtnAddCompany.IsEnabled = canAdd;
            BtnAddCompany.Opacity = canAdd ? 1 : 0.45;
            LblPlanLimit.Text = canAdd
                ? "Free plan: you can add 1 extra company"
                : "Free plan company limit reached (2 total). Upgrade will be required to add more.";

            CompanyCards.Children.Clear();
            foreach (var c in _items.OrderByDescending(x => x.IsCurrentCompany).ThenBy(x => x.Name))
                CompanyCards.Children.Add(BuildCard(c));

            if (_items.Count == 0)
                CompanyCards.Children.Add(new Label
                {
                    Text = "No companies found.",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#64748B"),
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 40)
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Company] Load failed: {ex}");
            CompanyCards.Children.Clear();
            CompanyCards.Children.Add(new Label
            {
                Text = "Companies could not be loaded. Pull down to retry.",
                FontSize = 13,
                TextColor = Color.FromArgb("#DC2626"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 32)
            });
        }
        finally { _loading = false; }
    }

    private async void OnRefreshing(object? s, EventArgs e)
    {
        try { await LoadForHostAsync(); }
        finally { Refresher.IsRefreshing = false; }
    }

    private View BuildCard(CompanyModel c)
    {
        var border = new Border
        {
            Style = (Style)Resources["ListRow"],
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 14
        };

        var avatar = new Border
        {
            WidthRequest = 46,
            HeightRequest = 46,
            BackgroundColor = Color.FromArgb("#EFF6FF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            VerticalOptions = LayoutOptions.Center
        };
        avatar.Content = new Label
        {
            Text = c.Initials,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2563EB"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var info = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(new Label { Text = c.Name, FontSize = 14, FontAttributes = FontAttributes.Bold });
        if (!string.IsNullOrWhiteSpace(c.Email))
            info.Children.Add(new Label { Text = c.Email, FontSize = 12, TextColor = Color.FromArgb("#64748B") });
        var location = string.Join(", ", new[] { c.City, c.Country }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (!string.IsNullOrWhiteSpace(location))
            info.Children.Add(new Label { Text = location, FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });

        var actions = new VerticalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End };
        if (c.IsCurrentCompany)
        {
            actions.Children.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#EFF6FF"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
                Padding = new Thickness(8, 3),
                Content = new Label { Text = "Current", FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#2563EB") }
            });
        }
        else
        {
            var switchBtn = new Button { Text = "Switch", Style = (Style)Resources["SmallSecondaryButton"] };
            switchBtn.Clicked += async (_, _) =>
            {
                await _svc.SwitchCompanyAsync(c.LocalId);
                await LoadForHostAsync();
            };
            actions.Children.Add(switchBtn);
        }

        var editBtn = new Button { Text = "Edit", Style = (Style)Resources["SmallButton"] };
        editBtn.Clicked += async (_, _) => await OpenEditAsync(c);
        actions.Children.Add(editBtn);

        grid.Add(avatar, 0, 0);
        grid.Add(info, 1, 0);
        grid.Add(actions, 2, 0);
        border.Content = grid;
        return border;
    }

    private async Task OpenEditAsync(CompanyModel company)
    {
        try
        {
            var page = ActivatorUtilities.CreateInstance<CompanyFormPage>(_services);
            page.EditId = company.LocalId.ToString("D");
            await _navigator.NavigateAsync(page, "Edit Company", "Home / Company / Edit");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Company] Edit navigation failed: {ex}");
            await DisplayAlert("Company", "Company could not be opened.", "OK");
        }
    }

    private async void OnAddClicked(object? s, EventArgs e)
    {
        if (_items.Count >= FreePlanTotalCompanyLimit)
        {
            await DisplayAlert("Free plan limit", "Free plan allows the company created during registration plus 1 additional company. Upgrade will be required to add more companies.", "OK");
            return;
        }

        try
        {
            var page = ActivatorUtilities.CreateInstance<CompanyFormPage>(_services);
            await _navigator.NavigateAsync(page, "Add Company", "Home / Company / Add");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Company] Add navigation failed: {ex}");
            await DisplayAlert("Company", "Add Company could not be opened.", "OK");
        }
    }
}
