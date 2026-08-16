using AveroNova.App.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.Pages;

public partial class CompanyPage : ContentPage
{
    private readonly CompanyViewModel _viewModel;
    public CompanyPage(CompanyViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    private async void SaveButton_Clicked(object sender, EventArgs e)
    {
        await _viewModel.SaveCompanyAsync();

        await DisplayAlertAsync("Success", "Company Saved Successfully", "OK");
    }
}