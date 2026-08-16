using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Customers;

[QueryProperty(nameof(CustomerId), "id")]
public partial class CustomerViewPage : ContentPage
{
    private readonly ICustomerService _svc;
    private CustomerModel? _customer;
    public string? CustomerId { get; set; }

    public CustomerViewPage(ICustomerService svc) { InitializeComponent(); _svc = svc; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(CustomerId) && Guid.TryParse(CustomerId, out var id))
        {
            _customer = await _svc.GetByIdAsync(id);
            if (_customer != null) BuildContent(_customer);
        }
    }

    private void BuildContent(CustomerModel c)
    {
        Content.Children.Clear();

        // Profile card
        var profileCard = new Border { Style = (Style)Resources["AppCard"] };
        var pGrid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 16 };
        var av = new Border { WidthRequest = 60, HeightRequest = 60, BackgroundColor = Color.FromArgb("#EFF6FF"), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(30) } };
        av.Content = new Label { Text = c.Initials, FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#2563EB"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        var info = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(new Label { Text = c.Name, FontSize = 18, FontAttributes = FontAttributes.Bold });
        info.Children.Add(new Label { Text = c.Email, FontSize = 13, TextColor = Color.FromArgb("#64748B") });
        info.Children.Add(new Label { Text = c.Phone, FontSize = 13, TextColor = Color.FromArgb("#64748B") });
        pGrid.Add(av,   0, 0);
        pGrid.Add(info, 1, 0);
        profileCard.Content = pGrid;

        // Stats
        var statsCard = new Border { Style = (Style)Resources["AppCard"] };
        var sGrid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 16 };
        sGrid.Add(BuildStat("Total Purchases", $"${c.TotalPurchases:N0}", "#2563EB"), 0, 0);
        sGrid.Add(BuildStat("Outstanding",     $"${c.OutstandingBalance:N0}", c.OutstandingBalance > 0 ? "#DC2626" : "#059669"), 1, 0);
        statsCard.Content = sGrid;

        // Details
        var detailCard = new Border { Style = (Style)Resources["AppCard"] };
        var dVsl = new VerticalStackLayout { Spacing = 12 };
        dVsl.Children.Add(new Label { Text = "Details", FontSize = 14, FontAttributes = FontAttributes.Bold });
        dVsl.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });
        void AddDetail(string label, string value) { dVsl.Children.Add(BuildDetailRow(label, value)); }
        AddDetail("Status",    c.StatusLabel);
        AddDetail("Address",   c.Address);
        AddDetail("City",      c.City);
        AddDetail("Country",   c.Country);
        AddDetail("Tax No.",   c.TaxNumber);
        if (!string.IsNullOrEmpty(c.Notes)) AddDetail("Notes", c.Notes);
        detailCard.Content = dVsl;

        Content.Children.Add(profileCard);
        Content.Children.Add(statsCard);
        Content.Children.Add(detailCard);
    }

    private static View BuildStat(string label, string value, string colorHex)
    {
        var vsl = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center };
        vsl.Children.Add(new Label { Text = value, FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(colorHex), HorizontalOptions = LayoutOptions.Center });
        vsl.Children.Add(new Label { Text = label, FontSize = 11, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center });
        return vsl;
    }

    private static View BuildDetailRow(string label, string value)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new GridLength(130)), new ColumnDefinition(GridLength.Star)) };
        g.Add(new Label { Text = label, FontSize = 13, TextColor = Color.FromArgb("#64748B") }, 0, 0);
        g.Add(new Label { Text = value, FontSize = 13, FontAttributes = FontAttributes.Bold },  1, 0);
        return g;
    }

    private async void OnEditClicked(object s, EventArgs e)   => await Shell.Current.GoToAsync($"{AppRoutes.CustomerEdit}?id={_customer?.LocalId}");
    private async void OnBackClicked(object s, EventArgs e)   => await Shell.Current.GoToAsync("..");

    private async void OnDeleteClicked(object s, EventArgs e)
    {
        if (_customer == null) return;
        if (!await DialogHelper.ConfirmDeleteAsync("Customer", $"Delete {_customer.Name}?")) return;
        await _svc.DeleteAsync(_customer.LocalId);
        await Shell.Current.GoToAsync("..");
    }
}
