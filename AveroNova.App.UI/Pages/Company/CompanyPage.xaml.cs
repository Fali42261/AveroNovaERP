using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.Company;

public partial class CompanyPage : ContentPage
{
    private readonly CompanyPageViewModel _vm;

    public CompanyPage(CompanyPageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        if (Content != null)
            Content.BindingContext = vm;
        ErrorState.RetryClicked += async (_, _) => await _vm.LoadAsync();
    }

    public Task ReloadAsync() => _vm.LoadAsync();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
