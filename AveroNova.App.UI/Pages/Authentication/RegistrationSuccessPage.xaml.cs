using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class RegistrationSuccessPage : ContentPage
{
    private readonly RegistrationWizardViewModel _wizard;

    public RegistrationSuccessPage(RegistrationWizardViewModel wizard)
    {
        InitializeComponent();
        _wizard = wizard;
    }

    private async void OnGoToLoginClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        await BusyButton.RunAsync(button, async () =>
        {
            _wizard.ResetForm();
            await Shell.Current.GoToAsync(AppRoutes.Login);
        });
    }
}
