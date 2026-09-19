using AveroNova.App.UI.ViewModels;
using AveroNova.App.UI.Navigation;

namespace AveroNova.App.UI.Pages.License;

public partial class LicensePage : ContentPage, IHostedPage
{
    private readonly LicenseViewModel _vm;

    public LicensePage(LicenseViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }

    public Task LoadForHostAsync() => _vm.LoadAsync();
}
