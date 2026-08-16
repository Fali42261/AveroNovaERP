using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class ForgotPasswordPage : ContentPage
{
    public ForgotPasswordPage(ForgotPasswordViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnBackClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    private async void OnGoToLoginClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        await BusyButton.RunAsync(button, async () =>
            await Shell.Current.GoToAsync(AppRoutes.Login));
    }
}
