using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Customers;

[QueryProperty(nameof(CustomerId), "id")]
public partial class CustomerViewPage : ContentPage, IHostedPage
{
    private readonly ICustomerService _svc;
    private readonly IMainContentNavigator _navigator;
    private readonly IServiceProvider _services;
    private CustomerModel? _customer;

    public string? CustomerId { get; set; }

    public CustomerViewPage(
        ICustomerService svc,
        IMainContentNavigator navigator,
        IServiceProvider services)
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
        try
        {
            if (!string.IsNullOrEmpty(CustomerId) && Guid.TryParse(CustomerId, out var id))
            {
                _customer = await _svc.GetByIdAsync(id);
                if (_customer != null)
                    BuildContent(_customer);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CustomerView] Load failed: {ex}");
            await DisplayAlert("Customer", "Customer details could not be loaded.", "OK");
        }
    }

    private void BuildContent(CustomerModel c)
    {
        Content.Children.Clear();

        var profileCard = new Border
        {
            Stroke = Color.FromArgb("#E2E8F0"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
            Padding = new Thickness(16),
            BackgroundColor = Colors.White
        };

        var pGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)),
            ColumnSpacing = 16
        };

        var av = new Border
        {
            WidthRequest = 60,
            HeightRequest = 60,
            BackgroundColor = Color.FromArgb("#EFF6FF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(30) },
            Content = new Label
            {
                Text = c.Initials,
                FontSize = 22,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#2563EB"),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };

        var info = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(new Label { Text = c.Name, FontSize = 18, FontAttributes = FontAttributes.Bold });
        info.Children.Add(new Label { Text = c.Email, FontSize = 13, TextColor = Color.FromArgb("#64748B") });
        info.Children.Add(new Label { Text = c.Phone, FontSize = 13, TextColor = Color.FromArgb("#64748B") });
        pGrid.Add(av, 0, 0);
        pGrid.Add(info, 1, 0);
        profileCard.Content = pGrid;

        var statsCard = MakeCard();
        var sGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)),
            ColumnSpacing = 16
        };
        sGrid.Add(BuildStat("Total Purchases", $"${c.TotalPurchases:N0}", "#2563EB"), 0, 0);
        sGrid.Add(BuildStat("Outstanding", $"${c.OutstandingBalance:N0}", c.OutstandingBalance > 0 ? "#DC2626" : "#059669"), 1, 0);
        statsCard.Content = sGrid;

        var detailCard = MakeCard();
        var dVsl = new VerticalStackLayout { Spacing = 12 };
        dVsl.Children.Add(new Label { Text = "Details", FontSize = 14, FontAttributes = FontAttributes.Bold });
        dVsl.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#E2E8F0") });
        dVsl.Children.Add(BuildDetailRow("Status", c.StatusLabel));
        dVsl.Children.Add(BuildDetailRow("Address", c.Address));
        dVsl.Children.Add(BuildDetailRow("City", c.City));
        dVsl.Children.Add(BuildDetailRow("Country", c.Country));
        dVsl.Children.Add(BuildDetailRow("Tax No.", c.TaxNumber));
        if (!string.IsNullOrEmpty(c.Notes)) dVsl.Children.Add(BuildDetailRow("Notes", c.Notes));
        detailCard.Content = dVsl;

        Content.Children.Add(profileCard);
        Content.Children.Add(statsCard);
        Content.Children.Add(detailCard);
    }

    private static Border MakeCard() => new()
    {
        Stroke = Color.FromArgb("#E2E8F0"),
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
        Padding = new Thickness(16),
        BackgroundColor = Colors.White
    };

    private static View BuildStat(string label, string value, string colorHex)
    {
        var vsl = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center };
        vsl.Children.Add(new Label { Text = value, FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(colorHex), HorizontalOptions = LayoutOptions.Center });
        vsl.Children.Add(new Label { Text = label, FontSize = 11, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center });
        return vsl;
    }

    private static View BuildDetailRow(string label, string value)
    {
        var g = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(new GridLength(130)),
                new ColumnDefinition(GridLength.Star))
        };
        g.Add(new Label { Text = label, FontSize = 13, TextColor = Color.FromArgb("#64748B") }, 0, 0);
        g.Add(new Label { Text = value, FontSize = 13, FontAttributes = FontAttributes.Bold }, 1, 0);
        return g;
    }

    private async void OnEditClicked(object s, EventArgs e)
    {
        if (_customer == null) return;
        var page = ActivatorUtilities.CreateInstance<CustomerFormPage>(_services);
        page.EditId = _customer.LocalId.ToString("D");
        await _navigator.NavigateAsync(page, "Edit Customer", "Home / Customers / Edit");
    }

    private async void OnBackClicked(object s, EventArgs e) => await _navigator.GoBackAsync();

    private async void OnDeleteClicked(object s, EventArgs e)
    {
        if (_customer == null) return;
        if (!await DialogHelper.ConfirmDeleteAsync("Customer", $"Delete {_customer.Name}?")) return;

        try
        {
            await _svc.DeleteAsync(_customer.LocalId);
            await _navigator.GoBackAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CustomerView] Delete failed: {ex}");
            await DisplayAlert("Customer", "Customer could not be deleted.", "OK");
        }
    }
}
