using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Views.Layout;

namespace AveroNova.App.UI;

public partial class MainPage : ContentPage
{
    private readonly ILicenseService _licenses;

    public MainPage(MainLayoutView layout, ILicenseService licenses)
    {
        InitializeComponent();
        _licenses = licenses;
        LayoutHost.Content = layout;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _licenses.ValidateOnlineIfPossibleAsync();
        await _licenses.SyncOnlineIfPossibleAsync();
    }
}
