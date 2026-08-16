using AveroNova.App.UI.ViewModels;
using AveroNova.App.UI.Views.Company;
using System.ComponentModel;

namespace AveroNova.App.UI.Pages.Company;

public partial class CompanySetupPage : ContentPage
{
    private readonly CompanySetupViewModel _viewModel;

    public CompanySetupPage(CompanySetupViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = _viewModel;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        UpdateStep();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CompanySetupViewModel.CurrentStep))
        {
            UpdateStep();
        }
    }

    private void UpdateStep()
    {
        CompanyFormView.IsVisible = (_viewModel.CurrentStep == 1);

        //TeamSetupView.IsVisible = (_viewModel.CurrentStep == 2);

        //AdminAccountView.IsVisible = (_viewModel.CurrentStep == 3);

        FinishSetupView.IsVisible = (_viewModel.CurrentStep == 2);
    }
}