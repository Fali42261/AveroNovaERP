namespace AveroNova.App.UI.Controls.Forms;

/// <summary>
/// Read-only field card that mirrors the edit-form visual language:
/// label on top and the value inside an input-style surface.
/// </summary>
public sealed class ReadOnlyFieldCard : Border
{
    public static readonly BindableProperty LabelTextProperty =
        BindableProperty.Create(nameof(LabelText), typeof(string), typeof(ReadOnlyFieldCard), string.Empty);

    public static readonly BindableProperty ValueTextProperty =
        BindableProperty.Create(nameof(ValueText), typeof(string), typeof(ReadOnlyFieldCard), string.Empty,
            propertyChanged: OnValueChanged);

    public static readonly BindableProperty IsMultilineProperty =
        BindableProperty.Create(nameof(IsMultiline), typeof(bool), typeof(ReadOnlyFieldCard), false,
            propertyChanged: OnMultilineChanged);

    private readonly Border _valueHost;
    private readonly Label _valueLabel;

    public ReadOnlyFieldCard()
    {
        Style = TryStyle("AppCard");
        Padding = new Thickness(16, 14);
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        MinimumWidthRequest = 0;

        var label = new Label
        {
            Style = TryStyle("InputLabel"),
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        };
        label.SetBinding(Label.TextProperty, new Binding(nameof(LabelText), source: this));

        _valueLabel = new Label
        {
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Fill,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
            TextColor = TryColor("TextPrimary", Color.FromArgb("#0F172A"))
        };
        _valueLabel.SetBinding(Label.TextProperty, new Binding(nameof(ValueText), source: this));

        _valueHost = new Border
        {
            Style = TryStyle("InputContainer"),
            MinimumHeightRequest = 46,
            Padding = new Thickness(14, 0),
            VerticalOptions = LayoutOptions.Fill,
            Content = _valueLabel
        };

        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add(label);
        stack.Children.Add(_valueHost);
        Content = stack;
        UpdateMultiline();
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

    public bool IsMultiline
    {
        get => (bool)GetValue(IsMultilineProperty);
        set => SetValue(IsMultilineProperty, value);
    }

    private static void OnValueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ReadOnlyFieldCard card)
            card.UpdateTooltip();
    }

    private static void OnMultilineChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ReadOnlyFieldCard card)
            card.UpdateMultiline();
    }

    private void UpdateMultiline()
    {
        _valueHost.MinimumHeightRequest = IsMultiline ? 88 : 46;
        _valueHost.Padding = IsMultiline ? new Thickness(14, 10) : new Thickness(14, 0);
        _valueLabel.VerticalOptions = IsMultiline ? LayoutOptions.Start : LayoutOptions.Center;
        _valueLabel.LineBreakMode = IsMultiline ? LineBreakMode.WordWrap : LineBreakMode.TailTruncation;
        _valueLabel.MaxLines = IsMultiline ? 4 : 1;
    }

    private void UpdateTooltip()
    {
        var text = ValueText?.Trim();
        ToolTipProperties.SetText(this, string.IsNullOrWhiteSpace(text) ? null : text);
        ToolTipProperties.SetText(_valueLabel, string.IsNullOrWhiteSpace(text) ? null : text);
    }

    private static Style? TryStyle(string key)
        => Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Style style
            ? style
            : null;

    private static Color TryColor(string key, Color fallback)
        => Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : fallback;
}
