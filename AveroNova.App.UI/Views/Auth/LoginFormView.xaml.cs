using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Views.Auth;

public partial class LoginFormView : ContentView
{
    public static readonly BindableProperty IsCreateAccountVisibleProperty =
        BindableProperty.Create(
            nameof(IsCreateAccountVisible),
            typeof(bool),
            typeof(LoginFormView),
            true,
            propertyChanged: static (bindable, _, value) =>
            {
                if (bindable is LoginFormView view)
                    view.CreateAccountRow.IsVisible = value is true;
            });

    public LoginFormView()
    {
        InitializeComponent();
    }

    public bool IsCreateAccountVisible
    {
        get => (bool)GetValue(IsCreateAccountVisibleProperty);
        set => SetValue(IsCreateAccountVisibleProperty, value);
    }

    public event EventHandler? SignInRequested;
    public event EventHandler? CreateAccountRequested;

    private void OnSignInClicked(object? sender, EventArgs e)
        => SignInRequested?.Invoke(this, e);

    private void OnCreateAccountTapped(object? sender, TappedEventArgs e)
        => CreateAccountRequested?.Invoke(this, e);
}
