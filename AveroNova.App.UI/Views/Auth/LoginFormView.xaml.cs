using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Views.Auth;

public partial class LoginFormView : ContentView
{
    public LoginFormView()
    {
        InitializeComponent();
    }

    // Events raised to the hosting Page so navigation stays in the Page layer
    public event EventHandler? SignInRequested;
    public event EventHandler? ForgotPasswordRequested;
    public event EventHandler? CreateAccountRequested;

    private void OnSignInClicked(object sender, EventArgs e)
        => SignInRequested?.Invoke(this, e);

    private void OnForgotPasswordTapped(object sender, TappedEventArgs e)
        => ForgotPasswordRequested?.Invoke(this, e);

    private void OnCreateAccountTapped(object sender, TappedEventArgs e)
        => CreateAccountRequested?.Invoke(this, e);
}
