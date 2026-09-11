using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.License;

public partial class LicensePage : ContentPage, IHostedPage
{
    private readonly LicenseViewModel _vm;
    private bool _loading;

    public LicensePage(LicenseViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
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
            await _vm.LoadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[License] Load failed: {ex}");
        }
        finally
        {
            _loading = false;
        }
    }
}
