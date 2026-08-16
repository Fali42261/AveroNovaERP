namespace AveroNova.App.UI.Controls.Cards;

public partial class StatCard : ContentView
{
    public StatCard()
    {
        InitializeComponent();
    }

    // ── Title ────────────────────────────────────────────────────────────────

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(StatCard), string.Empty);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    // ── Subtitle ─────────────────────────────────────────────────────────────

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(StatCard), string.Empty);

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    // ── Value (metric number) ─────────────────────────────────────────────────

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(string), typeof(StatCard), "0");

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    // ── Icon ─────────────────────────────────────────────────────────────────

    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(string), typeof(StatCard), string.Empty);

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    // ── IconBackground ────────────────────────────────────────────────────────

    public static readonly BindableProperty IconBackgroundProperty =
        BindableProperty.Create(nameof(IconBackground), typeof(Color), typeof(StatCard), Color.FromArgb("#EFF6FF"));

    public Color IconBackground
    {
        get => (Color)GetValue(IconBackgroundProperty);
        set => SetValue(IconBackgroundProperty, value);
    }

    // ── IconBorderColor ───────────────────────────────────────────────────────

    public static readonly BindableProperty IconBorderColorProperty =
        BindableProperty.Create(nameof(IconBorderColor), typeof(Color), typeof(StatCard), Color.FromArgb("#DBEAFE"));

    public Color IconBorderColor
    {
        get => (Color)GetValue(IconBorderColorProperty);
        set => SetValue(IconBorderColorProperty, value);
    }

    // ── BadgeText ─────────────────────────────────────────────────────────────

    public static readonly BindableProperty BadgeTextProperty =
        BindableProperty.Create(nameof(BadgeText), typeof(string), typeof(StatCard), "Active");

    public string BadgeText
    {
        get => (string)GetValue(BadgeTextProperty);
        set => SetValue(BadgeTextProperty, value);
    }

    // ── BadgeTextColor ────────────────────────────────────────────────────────

    public static readonly BindableProperty BadgeTextColorProperty =
        BindableProperty.Create(nameof(BadgeTextColor), typeof(Color), typeof(StatCard), Color.FromArgb("#2563EB"));

    public Color BadgeTextColor
    {
        get => (Color)GetValue(BadgeTextColorProperty);
        set => SetValue(BadgeTextColorProperty, value);
    }

    // ── BadgeBackground ───────────────────────────────────────────────────────

    public static readonly BindableProperty BadgeBackgroundProperty =
        BindableProperty.Create(nameof(BadgeBackground), typeof(Color), typeof(StatCard), Color.FromArgb("#EFF6FF"));

    public Color BadgeBackground
    {
        get => (Color)GetValue(BadgeBackgroundProperty);
        set => SetValue(BadgeBackgroundProperty, value);
    }

    // ── BadgeBorderColor ──────────────────────────────────────────────────────

    public static readonly BindableProperty BadgeBorderColorProperty =
        BindableProperty.Create(nameof(BadgeBorderColor), typeof(Color), typeof(StatCard), Color.FromArgb("#BFDBFE"));

    public Color BadgeBorderColor
    {
        get => (Color)GetValue(BadgeBorderColorProperty);
        set => SetValue(BadgeBorderColorProperty, value);
    }

    // ── Description ───────────────────────────────────────────────────────────

    public static readonly BindableProperty DescriptionProperty =
        BindableProperty.Create(nameof(Description), typeof(string), typeof(StatCard), string.Empty);

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
}
