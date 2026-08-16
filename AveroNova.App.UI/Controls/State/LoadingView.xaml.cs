namespace AveroNova.App.UI.Controls.State;

public partial class LoadingView : ContentView
{
    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(LoadingView), "Loading...",
            propertyChanged: (b, _, n) => ((LoadingView)b).LblMessage.Text = (string)n);

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public LoadingView() => InitializeComponent();
}
