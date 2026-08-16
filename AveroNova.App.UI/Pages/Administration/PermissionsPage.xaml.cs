using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Administration;

public partial class PermissionsPage : ContentPage
{
    public PermissionsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadContent();
    }

    private void OnRefreshing(object s, EventArgs e) { LoadContent(); Refresher.IsRefreshing = false; }
    private void OnRefreshClicked(object s, EventArgs e) { LoadContent(); }

    private void OnSearchChanged(object s, TextChangedEventArgs e)
    {
        LoadContent();
    }

    private void LoadContent()
    {
        PermissionsContent.Children.Clear();
        PermissionsContent.Children.Add(new Label { Text = "Permissions management coming soon.", FontSize = 14, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40) });
    }
}
