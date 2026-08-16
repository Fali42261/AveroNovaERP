using System.Reflection;

namespace AveroNova.App.UI.Behaviors;

public static class HandCursor
{
    public static readonly BindableProperty EnableProperty = BindableProperty.CreateAttached(
        "Enable",
        typeof(bool),
        typeof(HandCursor),
        false,
        propertyChanged: OnEnableChanged);

    public static bool GetEnable(BindableObject view) => (bool)view.GetValue(EnableProperty);

    public static void SetEnable(BindableObject view, bool value) => view.SetValue(EnableProperty, value);

    private static void OnEnableChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is not VisualElement element)
            return;

        element.HandlerChanged -= OnHandlerChanged;
        if (newValue is true)
        {
            element.HandlerChanged += OnHandlerChanged;
            Apply(element);
        }
    }

    private static void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is VisualElement element)
            Apply(element);
    }

    public static void Apply(VisualElement element)
    {
#if WINDOWS
        ApplyToPlatformView(element.Handler?.PlatformView);
#endif
    }

    public static void ApplyToPlatformView(object? platformView)
    {
#if WINDOWS
        if (platformView is not Microsoft.UI.Xaml.UIElement native)
            return;

        var cursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
        var property = typeof(Microsoft.UI.Xaml.UIElement).GetProperty(
            "ProtectedCursor",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property?.SetValue(native, cursor);
#endif
    }
}
