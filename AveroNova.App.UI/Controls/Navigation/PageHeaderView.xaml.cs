namespace AveroNova.App.UI.Controls.Navigation;

public partial class PageHeaderView : ContentView
{
    public static readonly BindableProperty TitleProperty    = BindableProperty.Create(nameof(Title),    typeof(string), typeof(PageHeaderView), string.Empty,   propertyChanged: (b, _, n) => ((PageHeaderView)b).LblTitle.Text    = (string)n);
    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(PageHeaderView), string.Empty,   propertyChanged: (b, _, n) => { var v = (PageHeaderView)b; v.LblSubtitle.Text = (string)n; v.LblSubtitle.IsVisible = !string.IsNullOrEmpty((string)n); });

    public string Title    { get => (string)GetValue(TitleProperty);    set => SetValue(TitleProperty,    value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    public PageHeaderView() => InitializeComponent();

    public void AddAction(View view) => ActionsContainer.Children.Add(view);
}
