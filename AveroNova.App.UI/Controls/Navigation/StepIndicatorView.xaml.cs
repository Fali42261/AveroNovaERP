using AveroNova.App.UI.Layout;

namespace AveroNova.App.UI.Controls.Navigation;

public partial class StepIndicatorView : ContentView
{
    public static readonly BindableProperty CurrentStepProperty = BindableProperty.Create(
        nameof(CurrentStep),
        typeof(int),
        typeof(StepIndicatorView),
        1,
        propertyChanged: (b, _, __) => ((StepIndicatorView)b).Apply());

    public int CurrentStep
    {
        get => (int)GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }

    public StepIndicatorView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Apply();
        Loaded += (_, _) => Apply();
    }

    private void Apply()
    {
        if (Step1Label is null)
            return;

        var compact = Width > 0 && Width < ResponsiveBreakpoints.ExpandedMinWidth;
        Step1Label.IsVisible = !compact;
        Step2Label.IsVisible = !compact;
        Step3Label.IsVisible = !compact;
        Step4Label.IsVisible = !compact;

        ApplyStep(1, Step1Badge, Step1Number);
        ApplyStep(2, Step2Badge, Step2Number);
        ApplyStep(3, Step3Badge, Step3Number);
        ApplyStep(4, Step4Badge, Step4Number);
    }

    private void ApplyStep(int index, Border badge, Label number)
    {
        var current = Math.Clamp(CurrentStep, 1, 4);
        var resources = Microsoft.Maui.Controls.Application.Current?.Resources;
        var active = resources?.TryGetValue("PrimaryColor", out var p) == true && p is Color pc
            ? pc
            : Color.FromArgb("#2563EB");
        var muted = resources?.TryGetValue("Gray200", out var g) == true && g is Color gc
            ? gc
            : Color.FromArgb("#E5E7EB");
        var white = Colors.White;
        var dark = resources?.TryGetValue("TextSecondary", out var t) == true && t is Color tc
            ? tc
            : Color.FromArgb("#64748B");

        if (index == current)
        {
            badge.BackgroundColor = active;
            number.TextColor = white;
        }
        else if (index < current)
        {
            badge.BackgroundColor = active;
            number.TextColor = white;
        }
        else
        {
            badge.BackgroundColor = muted;
            number.TextColor = dark;
        }
    }
}
