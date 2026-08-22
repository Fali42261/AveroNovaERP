namespace AveroNova.App.UI.Controls.Forms;

/// <summary>
/// Compact review card showing "Label : Value" on one line inside the AppCard design.
/// Long values truncate with ellipsis; the full value is available via tooltip.
/// </summary>
public sealed class CompactFieldCard : Border
{
    public static readonly BindableProperty LabelTextProperty =
        BindableProperty.Create(nameof(LabelText), typeof(string), typeof(CompactFieldCard), string.Empty);

    public static readonly BindableProperty ValueTextProperty =
        BindableProperty.Create(nameof(ValueText), typeof(string), typeof(CompactFieldCard), string.Empty,
            propertyChanged: OnValueTextChanged);

    private readonly Label _valueLabel;

    public CompactFieldCard()
    {
        Style = TryStyle("AppCard");
        Padding = new Thickness(14, 10);
        StrokeThickness = 1;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        MinimumWidthRequest = 0;

        var keyLabel = new Label
        {
            Style = TryStyle("ReviewFieldLabel"),
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.NoWrap
        };
        keyLabel.SetBinding(Label.TextProperty, new Binding(nameof(LabelText), source: this));

        var colonLabel = new Label
        {
            Text = ":",
            Style = TryStyle("ReviewFieldLabel"),
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.NoWrap
        };

        _valueLabel = new Label
        {
            Style = TryStyle("ReviewFieldValue"),
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Fill,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        };
        _valueLabel.SetBinding(Label.TextProperty, new Binding(nameof(ValueText), source: this));

        var grid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            ],
            ColumnSpacing = 6,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center
        };
        grid.Add(keyLabel, 0, 0);
        grid.Add(colonLabel, 1, 0);
        grid.Add(_valueLabel, 2, 0);
        Content = grid;
        UpdateTooltip();
    }

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    public string ValueText
    {
        get => (string)GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    private static void OnValueTextChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CompactFieldCard card)
            card.UpdateTooltip();
    }

    private void UpdateTooltip()
    {
        var text = ValueText;
        ToolTipProperties.SetText(this, string.IsNullOrWhiteSpace(text) ? null : text.Trim());
        ToolTipProperties.SetText(_valueLabel, string.IsNullOrWhiteSpace(text) ? null : text.Trim());
    }

    private static Style? TryStyle(string key)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is Style style)
        {
            return style;
        }

        return null;
    }
}
