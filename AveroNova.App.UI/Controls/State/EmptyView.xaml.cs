namespace AveroNova.App.UI.Controls.State;

public partial class EmptyView : ContentView
{
    public static readonly BindableProperty TitleProperty    = BindableProperty.Create(nameof(Title),    typeof(string), typeof(EmptyView), "Nothing here yet", propertyChanged: (b, _, n) => ((EmptyView)b).LblTitle.Text    = (string)n);
    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(EmptyView), "No records found.", propertyChanged: (b, _, n) => ((EmptyView)b).LblSubtitle.Text = (string)n);
    public static readonly BindableProperty IconProperty     = BindableProperty.Create(nameof(Icon),     typeof(string), typeof(EmptyView), "\U0001F4CB",        propertyChanged: (b, _, n) => ((EmptyView)b).LblIcon.Text     = (string)n);
    public static readonly BindableProperty ActionLabelProperty = BindableProperty.Create(nameof(ActionLabel), typeof(string), typeof(EmptyView), null, propertyChanged: (b, _, n) => { var v = (EmptyView)b; v.BtnAction.Text = (string)n; v.BtnAction.IsVisible = !string.IsNullOrEmpty((string)n); });

    public string  Title       { get => (string)GetValue(TitleProperty);    set => SetValue(TitleProperty,    value); }
    public string  Subtitle    { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string  Icon        { get => (string)GetValue(IconProperty);     set => SetValue(IconProperty,     value); }
    public string? ActionLabel { get => (string?)GetValue(ActionLabelProperty); set => SetValue(ActionLabelProperty, value); }

    public event EventHandler? ActionClicked;

    public EmptyView()
    {
        InitializeComponent();
        BtnAction.Clicked += (s, e) => ActionClicked?.Invoke(this, e);
    }
}
