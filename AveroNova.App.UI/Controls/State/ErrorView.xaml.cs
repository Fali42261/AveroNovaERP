namespace AveroNova.App.UI.Controls.State;

public partial class ErrorView : ContentView
{
    public static readonly BindableProperty TitleProperty   = BindableProperty.Create(nameof(Title),   typeof(string), typeof(ErrorView), "Something went wrong", propertyChanged: (b, _, n) => ((ErrorView)b).LblTitle.Text   = (string)n);
    public static readonly BindableProperty MessageProperty = BindableProperty.Create(nameof(Message), typeof(string), typeof(ErrorView), "Unable to load data.", propertyChanged: (b, _, n) => ((ErrorView)b).LblMessage.Text = (string)n);

    public string Title   { get => (string)GetValue(TitleProperty);   set => SetValue(TitleProperty,   value); }
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    public event EventHandler? RetryClicked;

    public ErrorView()
    {
        InitializeComponent();
        BtnRetry.Clicked += (s, e) => RetryClicked?.Invoke(this, e);
    }
}
