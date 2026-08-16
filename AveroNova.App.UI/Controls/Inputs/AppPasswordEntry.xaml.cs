namespace AveroNova.App.UI.Controls.Inputs;

public partial class AppPasswordEntry : ContentView
{
    public AppPasswordEntry()
    {
        InitializeComponent();
    }

    // ── Label ─────────────────────────────────────────────────────────────────

    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(AppPasswordEntry), string.Empty);

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    // ── HasLabel ──────────────────────────────────────────────────────────────

    public static readonly BindableProperty HasLabelProperty =
        BindableProperty.Create(nameof(HasLabel), typeof(bool), typeof(AppPasswordEntry), true);

    public bool HasLabel
    {
        get => (bool)GetValue(HasLabelProperty);
        set => SetValue(HasLabelProperty, value);
    }

    // ── Placeholder ───────────────────────────────────────────────────────────

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(AppPasswordEntry), "Enter password");

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    // ── Text ──────────────────────────────────────────────────────────────────

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(AppPasswordEntry), string.Empty, BindingMode.TwoWay);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // ── IsPasswordVisible ─────────────────────────────────────────────────────

    public static readonly BindableProperty IsPasswordVisibleProperty =
        BindableProperty.Create(
            nameof(IsPasswordVisible), typeof(bool), typeof(AppPasswordEntry), false,
            propertyChanged: OnPasswordVisibilityChanged);

    public bool IsPasswordVisible
    {
        get => (bool)GetValue(IsPasswordVisibleProperty);
        set => SetValue(IsPasswordVisibleProperty, value);
    }

    private static void OnPasswordVisibilityChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (AppPasswordEntry)bindable;
        var isVisible = (bool)newValue;
        // Eye icon: open eye when visible, closed (crossed) when hidden
        control.EyeIcon.Text = isVisible ? "\U0001F441" : "\U0001F576";
    }

    // ── HasError ──────────────────────────────────────────────────────────────

    public static readonly BindableProperty HasErrorProperty =
        BindableProperty.Create(
            nameof(HasError), typeof(bool), typeof(AppPasswordEntry), false,
            propertyChanged: OnHasErrorChanged);

    public bool HasError
    {
        get => (bool)GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    private static void OnHasErrorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (AppPasswordEntry)bindable;
        var hasError = (bool)newValue;
        control.InputBorder.Stroke = hasError
            ? Color.FromArgb("#EF4444")
            : Color.FromArgb("#E2E8F0");
    }

    // ── ErrorMessage ──────────────────────────────────────────────────────────

    public static readonly BindableProperty ErrorMessageProperty =
        BindableProperty.Create(nameof(ErrorMessage), typeof(string), typeof(AppPasswordEntry), string.Empty);

    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    // ── Toggle Handler ────────────────────────────────────────────────────────

    private void OnTogglePassword(object? sender, TappedEventArgs e)
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    // ── Focus/Blur visual feedback ────────────────────────────────────────────

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        PasswordEntryControl.Focused += (s, e) =>
        {
            if (!HasError)
                InputBorder.Stroke = Color.FromArgb("#2563EB");
        };
        PasswordEntryControl.Unfocused += (s, e) =>
        {
            if (!HasError)
                InputBorder.Stroke = Color.FromArgb("#E2E8F0");
        };
    }
}
