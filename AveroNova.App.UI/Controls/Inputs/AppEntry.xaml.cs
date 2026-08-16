namespace AveroNova.App.UI.Controls.Inputs;

public partial class AppEntry : ContentView
{
    public AppEntry()
    {
        InitializeComponent();
    }

    // ── Label ─────────────────────────────────────────────────────────────────

    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(AppEntry), string.Empty);

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    // ── HasLabel ──────────────────────────────────────────────────────────────

    public static readonly BindableProperty HasLabelProperty =
        BindableProperty.Create(nameof(HasLabel), typeof(bool), typeof(AppEntry), true);

    public bool HasLabel
    {
        get => (bool)GetValue(HasLabelProperty);
        set => SetValue(HasLabelProperty, value);
    }

    // ── Placeholder ───────────────────────────────────────────────────────────

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(AppEntry), string.Empty);

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    // ── Text ──────────────────────────────────────────────────────────────────

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(AppEntry), string.Empty, BindingMode.TwoWay);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // ── Icon ─────────────────────────────────────────────────────────────────

    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(string), typeof(AppEntry), string.Empty);

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    // ── HasIcon ───────────────────────────────────────────────────────────────

    public static readonly BindableProperty HasIconProperty =
        BindableProperty.Create(nameof(HasIcon), typeof(bool), typeof(AppEntry), false);

    public bool HasIcon
    {
        get => (bool)GetValue(HasIconProperty);
        set => SetValue(HasIconProperty, value);
    }

    // ── IsPassword ────────────────────────────────────────────────────────────

    public static readonly BindableProperty IsPasswordProperty =
        BindableProperty.Create(nameof(IsPassword), typeof(bool), typeof(AppEntry), false);

    public bool IsPassword
    {
        get => (bool)GetValue(IsPasswordProperty);
        set => SetValue(IsPasswordProperty, value);
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    public static readonly BindableProperty KeyboardProperty =
        BindableProperty.Create(nameof(Keyboard), typeof(Keyboard), typeof(AppEntry), Keyboard.Default);

    public Keyboard Keyboard
    {
        get => (Keyboard)GetValue(KeyboardProperty);
        set => SetValue(KeyboardProperty, value);
    }

    // ── IsReadOnly ────────────────────────────────────────────────────────────

    public static readonly BindableProperty IsReadOnlyProperty =
        BindableProperty.Create(nameof(IsReadOnly), typeof(bool), typeof(AppEntry), false);

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    // ── IsRequired ────────────────────────────────────────────────────────────

    public static readonly BindableProperty IsRequiredProperty =
        BindableProperty.Create(nameof(IsRequired), typeof(bool), typeof(AppEntry), false);

    public bool IsRequired
    {
        get => (bool)GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    // ── HasError ──────────────────────────────────────────────────────────────

    public static readonly BindableProperty HasErrorProperty =
        BindableProperty.Create(nameof(HasError), typeof(bool), typeof(AppEntry), false,
            propertyChanged: OnHasErrorChanged);

    public bool HasError
    {
        get => (bool)GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    private static void OnHasErrorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (AppEntry)bindable;
        control.InputBorder.Stroke = (bool)newValue
            ? Color.FromArgb("#EF4444")
            : Color.FromArgb("#E2E8F0");
    }

    // ── ErrorMessage ──────────────────────────────────────────────────────────

    public static readonly BindableProperty ErrorMessageProperty =
        BindableProperty.Create(nameof(ErrorMessage), typeof(string), typeof(AppEntry), string.Empty);

    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    // ── Focus / Blur visual feedback ──────────────────────────────────────────

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        EntryControl.Focused += (s, e) =>
        {
            if (!HasError)
                InputBorder.Stroke = Color.FromArgb("#2563EB");
        };
        EntryControl.Unfocused += (s, e) =>
        {
            if (!HasError)
                InputBorder.Stroke = Color.FromArgb("#E2E8F0");
        };
    }
}
