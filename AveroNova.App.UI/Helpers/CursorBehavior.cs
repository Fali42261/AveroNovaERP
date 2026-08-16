using System;
using System.Reflection;
using Microsoft.Maui.Controls;

#if WINDOWS
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
#endif

namespace AveroNova.App.UI.Helpers
{
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
            if (bindable is VisualElement visualElement)
            {
                var cursor = (CursorType)newValue;

                visualElement.Loaded -= VisualElement_Loaded;
                visualElement.Loaded += VisualElement_Loaded;

                visualElement.HandlerChanged -= VisualElement_HandlerChanged;
                visualElement.HandlerChanged += VisualElement_HandlerChanged;

                if (visualElement.Handler?.PlatformView != null)
                {
                    ApplyCursor(visualElement, cursor);
                }

                if (bindable is View view && cursor != CursorType.Default)
                {
                    var pointerGesture = new PointerGestureRecognizer();
                    pointerGesture.PointerEntered += (s, e) => ApplyCursor(visualElement, cursor);
                    pointerGesture.PointerExited += (s, e) => ApplyCursor(visualElement, CursorType.Arrow);
                    view.GestureRecognizers.Add(pointerGesture);
                }
            }
        }

        private static void VisualElement_Loaded(object? sender, EventArgs e)
        {
            if (sender is VisualElement visualElement)
            {
                ApplyCursor(visualElement, GetCursor(visualElement));
            }
        }

        private static void VisualElement_HandlerChanged(object? sender, EventArgs e)
        {
            if (sender is VisualElement visualElement)
            {
                ApplyCursor(visualElement, GetCursor(visualElement));
            }
        }

#if WINDOWS
        private static readonly PropertyInfo? ProtectedCursorProperty = 
            typeof(Microsoft.UI.Xaml.UIElement).GetProperty("ProtectedCursor", 
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
#endif

        public static void ApplyCursor(VisualElement element, CursorType cursorType)
        {
#if WINDOWS
            if (element.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement platformView && ProtectedCursorProperty != null)
            {
                try
                {
                    InputCursor? cursor = cursorType switch
                    {
                        CursorType.Hand => InputSystemCursor.Create(InputSystemCursorShape.Hand),
                        CursorType.IBeam => InputSystemCursor.Create(InputSystemCursorShape.IBeam),
                        CursorType.Arrow => InputSystemCursor.Create(InputSystemCursorShape.Arrow),
                        _ => null
                    };

                    ProtectedCursorProperty.SetValue(platformView, cursor);
                }
                catch
                {
                    // Fail gracefully if platform view is in intermediate state
                }
            }
#endif
        }
    }
}
