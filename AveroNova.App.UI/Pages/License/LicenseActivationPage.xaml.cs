using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.License;

public partial class LicenseActivationPage : ContentPage
{
    private readonly ILicenseService _licenses;
    private readonly IInstallationService _installation;
    private bool _busy;

    public LicenseActivationPage(ILicenseService licenses, IInstallationService installation)
    {
        InitializeComponent();
        _licenses = licenses;
        _installation = installation;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var access = await _licenses.GetAccessStateAsync();
        MessageLabel.Text = string.IsNullOrWhiteSpace(access.Message)
            ? "The trial or license on this device is not currently active."
            : access.Message;
        if (access.AllowsAccess)
            await ContinueAsync();
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;

        _busy = true;
        RetryButton.IsEnabled = false;
        try
        {
            await _licenses.SyncOnlineIfPossibleAsync();
            var status = await _licenses.EnsureActivatedAsync();
            if (status == LicenseBootstrapStatus.Ready)
            {
                await ContinueAsync();
                return;
            }

            var access = await _licenses.GetAccessStateAsync();
            MessageLabel.Text = access.Message
                ?? "This license is not currently active. You can keep using locally saved data once a valid license is available.";
        }
        finally
        {
            _busy = false;
            RetryButton.IsEnabled = true;
        }
    }

    private async Task ContinueAsync()
    {
        await _installation.EnsureInitializedAsync();
        await Shell.Current.GoToAsync(_installation.IsRegistered ? AppRoutes.Login : AppRoutes.Welcome);
    }
}
