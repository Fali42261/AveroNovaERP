using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Splash;

public partial class SplashPage : ContentPage
{
    private readonly IAuthenticationService _auth;
    private bool _isRunning;
    private bool _startupCompleted;

    public SplashPage(IAuthenticationService auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RunStartupAsync();
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
        => await RunStartupAsync();

    private async Task RunStartupAsync()
    {
        if (_isRunning || _startupCompleted)
            return;

        _isRunning = true;
        ShowLoading("Starting AveroNova...");

        try
        {
            await Task.Yield();

            LblStatus.Text = "Checking connection...";
            await Task.Delay(250);

            LblStatus.Text = "Checking account...";
            var autoLogin = false;
            if (NetworkStatus.HasInternet)
                autoLogin = await _auth.TryAutoLoginAsync();

            _startupCompleted = true;
            await Shell.Current.GoToAsync(autoLogin ? AppRoutes.Main : AppRoutes.Welcome);
        }
        catch
        {
            ShowError(UserMessages.StartupFailed);
        }
        finally
        {
            _isRunning = false;
        }
    }

    private void ShowLoading(string status)
    {
        LoadingPanel.IsVisible = true;
        ErrorPanel.IsVisible = false;
        Spinner.IsRunning = true;
        LblStatus.Text = status;
    }

    private void ShowError(string message)
    {
        Spinner.IsRunning = false;
        LoadingPanel.IsVisible = false;
        ErrorPanel.IsVisible = true;
        LblError.Text = message;
    }
}
