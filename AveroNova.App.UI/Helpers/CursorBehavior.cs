using System.Reflection;

namespace AveroNova.App.UI.Helpers;

public enum CursorType
{
    Default,
    Hand,
    Arrow,
    IBeam
}

public static class CursorBehavior
{
    public static readonly BindableProperty CursorProperty =
        BindableProperty.CreateAttached(
            "Cursor",
            typeof(CursorType),
            typeof(CursorBehavior),
            CursorType.Default,
            propertyChanged: OnCursorChanged);

    public static CursorType GetCursor(BindableObject view) => (CursorType)view.GetValue(CursorProperty);
    public static void SetCursor(BindableObject view, CursorType value) => view.SetValue(CursorProperty, value);

    private static void OnCursorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not VisualElement visualElement)
            return;

        visualElement.Loaded -= VisualElement_Loaded;
        visualElement.Loaded += VisualElement_Loaded;
        visualElement.HandlerChanged -= VisualElement_HandlerChanged;
        visualElement.HandlerChanged += VisualElement_HandlerChanged;

        ApplyCursor(visualElement, (CursorType)newValue);
    }

    private static void VisualElement_Loaded(object? sender, EventArgs e)
    {
        if (sender is VisualElement visualElement)
            ApplyCursor(visualElement, GetCursor(visualElement));
    }

    private static void VisualElement_HandlerChanged(object? sender, EventArgs e)
    {
        if (sender is VisualElement visualElement)
            ApplyCursor(visualElement, GetCursor(visualElement));
    }

    public static void ApplyCursor(VisualElement element, CursorType cursorType)
    {
#if WINDOWS
        if (_applying)
            return;

        if (element.Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement platformView)
            return;

        _applying = true;
        try
        {
            SetProtectedCursor(platformView, ToNativeCursor(cursorType));
        }
        finally
        {
            _applying = false;
        }
#else
        _ = element;
        _ = cursorType;
#endif
    }

#if WINDOWS
    [ThreadStatic]
    private static bool _applying;

    private static readonly PropertyInfo? ProtectedCursorProperty =
        typeof(Microsoft.UI.Xaml.UIElement).GetProperty(
            "ProtectedCursor",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly Microsoft.UI.Input.InputCursor HandCursor =
        Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);

    private static readonly Microsoft.UI.Input.InputCursor ArrowCursor =
        Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);

    private static readonly Microsoft.UI.Input.InputCursor IBeamCursor =
        Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.IBeam);

    private static Microsoft.UI.Input.InputCursor? ToNativeCursor(CursorType cursorType) => cursorType switch
    {
        CursorType.Hand => HandCursor,
        CursorType.IBeam => IBeamCursor,
        CursorType.Arrow => ArrowCursor,
        _ => null
    };

    private static void SetProtectedCursor(Microsoft.UI.Xaml.UIElement platformView, Microsoft.UI.Input.InputCursor? cursor)
    {
        if (ProtectedCursorProperty is null)
            return;

        try
        {
            ProtectedCursorProperty.SetValue(platformView, cursor);
        }
        catch
        {
            // Platform view can be in an intermediate state during handler attach.
        }
    }
#endif
}
