namespace AveroNova.App.UI.Controls.Forms;

/// <summary>
/// Review field that shows "Label : Value" on wide layouts and stacked
/// label-then-value on compact widths. Theme colors come from existing styles.
/// </summary>
public sealed class ReviewFieldRow : Grid
{
    public const double DefaultLabelColumnWidth = 148;

    public static readonly BindableProperty LabelTextProperty =
        BindableProperty.Create(nameof(LabelText), typeof(string), typeof(ReviewFieldRow), string.Empty,
            propertyChanged: (_, _, _) => { });

    public static readonly BindableProperty ValueTextProperty =
        BindableProperty.Create(nameof(ValueText), typeof(string), typeof(ReviewFieldRow), string.Empty);

    public static readonly BindableProperty IsInlineProperty =
        BindableProperty.Create(nameof(IsInline), typeof(bool), typeof(ReviewFieldRow), true,
            propertyChanged: OnLayoutPropertyChanged);

    public static readonly BindableProperty LabelColumnWidthProperty =
        BindableProperty.Create(nameof(LabelColumnWidth), typeof(double), typeof(ReviewFieldRow), DefaultLabelColumnWidth,
            propertyChanged: OnLayoutPropertyChanged);

    private readonly Label _caption;
    private readonly Label _colon;
    private readonly Label _value;

    public ReviewFieldRow()
    {
        ColumnSpacing = 8;
        RowSpacing = 2;
        HorizontalOptions = LayoutOptions.Fill;
        Padding = 0;

        _caption = new Label
        {
            Style = TryStyle("ReviewFieldLabel"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Start
        };
        _caption.SetBinding(Label.TextProperty, new Binding(nameof(LabelText), source: this));

        _colon = new Label
        {
            Text = ":",
            Style = TryStyle("ReviewFieldLabel"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Start
        };
        SemanticProperties.SetDescription(_colon, string.Empty);

        _value = new Label
        {
            Style = TryStyle("ReviewFieldValue"),
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };
        _value.SetBinding(Label.TextProperty, new Binding(nameof(ValueText), source: this));

        Children.Add(_caption);
        Children.Add(_colon);
        Children.Add(_value);
        ApplyPresentation();
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

    public bool IsInline
    {
        get => (bool)GetValue(IsInlineProperty);
        set => SetValue(IsInlineProperty, value);
    }

    public double LabelColumnWidth
    {
        get => (double)GetValue(LabelColumnWidthProperty);
        set => SetValue(LabelColumnWidthProperty, value);
    }

    private static void OnLayoutPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ReviewFieldRow row)
            row.ApplyPresentation();
    }

    private void ApplyPresentation()
    {
        if (IsInline)
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(new GridLength(LabelColumnWidth)),
                new(GridLength.Auto),
                new(GridLength.Star)
            };
            RowDefinitions = new RowDefinitionCollection { new(GridLength.Auto) };

            _caption.LineBreakMode = LineBreakMode.WordWrap;
            _colon.IsVisible = true;
            SetCell(_caption, 0, 0);
            SetCell(_colon, 1, 0);
            SetCell(_value, 2, 0);
        }
        else
        {
            ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star) };
            RowDefinitions = new RowDefinitionCollection
            {
                new(GridLength.Auto),
                new(GridLength.Auto)
            };

            _caption.LineBreakMode = LineBreakMode.WordWrap;
            _caption.WidthRequest = -1;
            _colon.IsVisible = false;
            SetCell(_caption, 0, 0);
            SetCell(_colon, 0, 0);
            SetCell(_value, 0, 1);
        }
    }

    private static void SetCell(View view, int column, int row)
    {
        SetColumn(view, column);
        SetRow(view, row);
        SetColumnSpan(view, 1);
        SetRowSpan(view, 1);
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
