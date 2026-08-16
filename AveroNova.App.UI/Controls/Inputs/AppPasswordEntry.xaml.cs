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
        var eye = ResolveIcon("IconAuthEye", "\u25CE");
        var eyeOff = ResolveIcon("IconAuthEyeOff", "\u2299");
        control.EyeIcon.Text = isVisible ? eyeOff : eye;
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
            ? (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ErrorColor"]
            : Colors.Transparent;
        control.InputBorder.StrokeThickness = hasError ? 1 : 0;
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
            {
                InputBorder.Stroke = (Color)Microsoft.Maui.Controls.Application.Current!.Resources["PrimaryColor"];
                InputBorder.StrokeThickness = 1;
            }
        };
        PasswordEntryControl.Unfocused += (s, e) =>
        {
            if (!HasError)
            {
                InputBorder.Stroke = Colors.Transparent;
                InputBorder.StrokeThickness = 0;
            }
        };
    }

    private static string ResolveIcon(string key, string fallback)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is string text
            && !string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return fallback;
    }
}
