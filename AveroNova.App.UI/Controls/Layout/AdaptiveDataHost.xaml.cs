using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Controls.Layout;

public partial class AdaptiveDataHost : ContentView
{
    public static readonly BindableProperty TableContentProperty = BindableProperty.Create(
        nameof(TableContent),
        typeof(View),
        typeof(AdaptiveDataHost),
        propertyChanged: OnContentChanged);

    public static readonly BindableProperty CardContentProperty = BindableProperty.Create(
        nameof(CardContent),
        typeof(View),
        typeof(AdaptiveDataHost),
        propertyChanged: OnContentChanged);

    private IResponsiveLayoutService? _layout;

    public View? TableContent
    {
        get => (View?)GetValue(TableContentProperty);
        set => SetValue(TableContentProperty, value);
    }

    public View? CardContent
    {
        get => (View?)GetValue(CardContentProperty);
        set => SetValue(CardContentProperty, value);
    }

    public AdaptiveDataHost()
    {
        InitializeComponent();
        SizeChanged += (_, _) => ApplyPresentation();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (_layout != null)
            _layout.LayoutChanged -= OnLayoutChanged;

        _layout = Handler?.MauiContext?.Services.GetService<IResponsiveLayoutService>();
        if (_layout != null)
            _layout.LayoutChanged += OnLayoutChanged;

        ApplyPresentation();
    }

    private void OnLayoutChanged(object? sender, EventArgs e) => ApplyPresentation();

    private static void OnContentChanged(BindableObject bindable, object? oldValue, object? newValue)
        => ((AdaptiveDataHost)bindable).ApplyPresentation();

    private void ApplyPresentation()
    {
        if (TableSlot is null || CardSlot is null)
            return;

        TableSlot.Content = TableContent;
        CardSlot.Content = CardContent;

        var useTable = _layout?.UseTableLayout
                       ?? Width >= AveroNova.App.UI.Layout.ResponsiveBreakpoints.ExpandedMinWidth;

        TableSlot.IsVisible = useTable && TableContent != null;
        CardSlot.IsVisible = !useTable && CardContent != null;
    }
}
