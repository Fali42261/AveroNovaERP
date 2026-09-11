using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Customers;

public partial class CustomersListPage : ContentPage, IHostedPage
{
    private readonly ICustomerService _svc;
    private readonly ICompanyService _company;
    private readonly IMainContentNavigator _navigator;
    private readonly IServiceProvider _services;
    private List<CustomerModel> _all = [];
    private List<CustomerModel> _shown = [];

    public CustomersListPage(
        ICustomerService svc,
        ICompanyService company,
        IMainContentNavigator navigator,
        IServiceProvider services)
    {
        InitializeComponent();
        _svc = svc;
        _company = company;
        _navigator = navigator;
        _services = services;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await SafeLoadAsync();
    }

    public Task LoadForHostAsync() => SafeLoadAsync();

    private async void OnRefreshing(object s, EventArgs e)
    {
        await SafeLoadAsync();
        Refresher.IsRefreshing = false;
    }

    private async void OnRefreshClicked(object s, EventArgs e) => await SafeLoadAsync();

    private async Task SafeLoadAsync()
    {
        try
        {
            var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty;
            _all = await _svc.GetAllAsync(cid);
            RenderList(_all);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Customers] Load failed: {ex}");
            _all = [];
            RenderList(_all);
            await DisplayAlert("Customers", "Customers could not be loaded. Your app is still running and local data is safe.", "OK");
        }
    }

    private async void OnSearchChanged(object s, TextChangedEventArgs e)
    {
        try
        {
            var q = e.NewTextValue?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(q))
            {
                RenderList(_all);
                return;
            }

            var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty;
            _shown = await _svc.SearchAsync(cid, q);
            RenderList(_shown);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Customers] Search failed: {ex}");
        }
    }

    private void RenderList(List<CustomerModel> items)
    {
        LblCount.Text = $"{items.Count} customer{(items.Count == 1 ? string.Empty : "s")}";
        CustomerList.Children.Clear();

        if (items.Count == 0)
        {
            CustomerList.Children.Add(new Label
            {
                Text = "No customers found.",
                FontSize = 14,
                TextColor = Color.FromArgb("#64748B"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 40)
            });
            return;
        }

        foreach (var c in items)
            CustomerList.Children.Add(BuildRow(c));
    }

    private View BuildRow(CustomerModel c)
    {
        var border = new Border
        {
            BackgroundColor = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#1E293B")
                : Colors.White,
            Stroke = Color.FromArgb("#E2E8F0"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Padding = new Thickness(14, 12)
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 12
        };

        var av = new Border
        {
            WidthRequest = 42,
            HeightRequest = 42,
            BackgroundColor = Color.FromArgb("#EFF6FF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(21) },
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = c.Initials,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#2563EB"),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };

        var info = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(new Label { Text = c.Name, FontSize = 14, FontAttributes = FontAttributes.Bold });
        info.Children.Add(new Label { Text = c.Email, FontSize = 12, TextColor = Color.FromArgb("#64748B") });
        info.Children.Add(new Label { Text = c.Phone, FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });

        var right = new VerticalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };

        var statusBadge = new Border
        {
            BackgroundColor = c.Status == CustomerStatus.Active ? Color.FromArgb("#ECFDF5") : Color.FromArgb("#FEF2F2"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            Padding = new Thickness(7, 2),
            Content = new Label
            {
                Text = c.StatusLabel,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = c.Status == CustomerStatus.Active ? Color.FromArgb("#059669") : Color.FromArgb("#DC2626")
            }
        };
        right.Children.Add(statusBadge);

        if (c.OutstandingBalance > 0)
        {
            right.Children.Add(new Label
            {
                Text = $"${c.OutstandingBalance:N0} due",
                FontSize = 11,
                TextColor = Color.FromArgb("#D97706"),
                HorizontalOptions = LayoutOptions.End
            });
        }

        var actionsRow = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.End };
        var viewBtn = new Button { Text = "View", FontSize = 12, HeightRequest = 36, Padding = new Thickness(12, 0) };
        viewBtn.Clicked += async (_, _) =>
        {
            var page = ActivatorUtilities.CreateInstance<CustomerViewPage>(_services);
            page.CustomerId = c.LocalId.ToString("D");
            await _navigator.NavigateAsync(page, "Customer Details", "Home / Customers / Details");
        };

        var editBtn = new Button { Text = "Edit", FontSize = 12, HeightRequest = 36, Padding = new Thickness(12, 0) };
        editBtn.Clicked += async (_, _) =>
        {
            var page = ActivatorUtilities.CreateInstance<CustomerFormPage>(_services);
            page.EditId = c.LocalId.ToString("D");
            await _navigator.NavigateAsync(page, "Edit Customer", "Home / Customers / Edit");
        };

        actionsRow.Children.Add(viewBtn);
        actionsRow.Children.Add(editBtn);
        right.Children.Add(actionsRow);

        grid.Add(av, 0, 0);
        grid.Add(info, 1, 0);
        grid.Add(right, 2, 0);
        border.Content = grid;
        return border;
    }

    private async void OnAddClicked(object s, EventArgs e)
    {
        try
        {
            var page = ActivatorUtilities.CreateInstance<CustomerFormPage>(_services);
            await _navigator.NavigateAsync(page, "Add Customer", "Home / Customers / Add");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Customers] Open add page failed: {ex}");
            await DisplayAlert("Customers", "Add Customer could not be opened. Please try again.", "OK");
        }
    }

    private void OnFilterClicked(object s, EventArgs e) =>
        DisplayAlert("Filter", "Filter options coming soon.", "OK");
}
