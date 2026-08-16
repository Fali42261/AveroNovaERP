using System.ComponentModel;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class RegisterPage : ContentPage
{
    private readonly RegistrationWizardViewModel _wizard;

    public RegisterPage(RegistrationWizardViewModel wizard)
    {
        InitializeComponent();
        _wizard = wizard;
        BindingContext = wizard;

        if (_wizard.CurrentStep < 2)
            _wizard.CurrentStep = 2;

        _wizard.PropertyChanged += OnWizardPropertyChanged;
        ApplyVisibleStep(_wizard.CurrentStep);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is null)
            _wizard.PropertyChanged -= OnWizardPropertyChanged;
    }

    private void OnWizardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RegistrationWizardViewModel.CurrentStep))
            ApplyVisibleStep(_wizard.CurrentStep);
    }

    private void ApplyVisibleStep(int currentStep)
    {
        var step = currentStep <= 1 ? 2 : currentStep;
        var indicatorStep = Math.Clamp(step - 1, 1, 4);

        Step1Container.IsVisible = step == 2;
        Step2Container.IsVisible = step == 3;
        Step3Container.IsVisible = step == 4;
        Step4Container.IsVisible = step >= 5;
        StepIndicator.CurrentStep = indicatorStep;

        LblStepCaption.Text = indicatorStep switch
        {
            1 => "Your details",
            2 => "Company details",
            3 => "Subscription",
            _ => "Review"
        };
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (_wizard.CurrentStep <= 2)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        _wizard.BackCommand.Execute(null);
    }
}
