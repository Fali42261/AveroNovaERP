using Microsoft.Maui.Handlers;

namespace AveroNova.App.UI.Helpers;

/// <summary>
/// Strips native Entry/Editor/SearchBar/Picker chrome so AveroNova input containers own the visuals.
/// </summary>
public static class NativeInputChrome
{
    public static void Register()
    {
        EntryHandler.Mapper.AppendToMapping("AveroNovaInputChrome", (handler, _) =>
            Apply(handler.PlatformView));
        EditorHandler.Mapper.AppendToMapping("AveroNovaInputChrome", (handler, _) =>
            Apply(handler.PlatformView));
        SearchBarHandler.Mapper.AppendToMapping("AveroNovaInputChrome", (handler, _) =>
            Apply(handler.PlatformView));
        PickerHandler.Mapper.AppendToMapping("AveroNovaInputChrome", (handler, _) =>
            Apply(handler.PlatformView));
        DatePickerHandler.Mapper.AppendToMapping("AveroNovaInputChrome", (handler, _) =>
            Apply(handler.PlatformView));

        EntryHandler.Mapper.AppendToMapping("AveroNovaInputFocus", (handler, view) =>
            AttachFocus(view as VisualElement));
        EditorHandler.Mapper.AppendToMapping("AveroNovaInputFocus", (handler, view) =>
            AttachFocus(view as VisualElement));
        SearchBarHandler.Mapper.AppendToMapping("AveroNovaInputFocus", (handler, view) =>
            AttachFocus(view as VisualElement));
        PickerHandler.Mapper.AppendToMapping("AveroNovaInputFocus", (handler, view) =>
            AttachFocus(view as VisualElement));
    }

    private static void AttachFocus(VisualElement? element)
    {
        if (element is not InputView input)
            return;

        input.Focused -= OnInputFocused;
        input.Unfocused -= OnInputUnfocused;
        input.Focused += OnInputFocused;
        input.Unfocused += OnInputUnfocused;
    }

    private static void OnInputFocused(object? sender, FocusEventArgs e)
        => SetContainerFocus(sender as View, true);

    private static void OnInputUnfocused(object? sender, FocusEventArgs e)
        => SetContainerFocus(sender as View, false);

    private static void SetContainerFocus(View? view, bool focused)
    {
        var border = FindInputBorder(view);
        if (border is null)
            return;

        var error = TryGetColor("ErrorColor");
        if (!focused && border.Stroke is SolidColorBrush current && error is not null && current.Color == error)
            return;

        if (focused)
        {
            border.Stroke = Colors.Transparent;
            border.StrokeThickness = 0;
        }
        else
        {
            border.Stroke = Colors.Transparent;
            border.StrokeThickness = 0;
        }
    }

    private static Border? FindInputBorder(Element? element)
    {
        while (element is not null)
        {
            if (element is Border border)
                return border;
            element = element.Parent;
        }

        return null;
    }

    private static Color? TryGetColor(string key)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is Color color)
        {
            return color;
        }

        return null;
    }

    private static void Apply(object? platformView)
    {
        if (platformView is null)
            return;

#if WINDOWS
        if (platformView is Microsoft.UI.Xaml.Controls.Control control)
        {
            control.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            control.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            control.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            control.FocusVisualPrimaryThickness = new Microsoft.UI.Xaml.Thickness(0);
            control.FocusVisualSecondaryThickness = new Microsoft.UI.Xaml.Thickness(0);
            control.UseSystemFocusVisuals = false;
            control.Padding = new Microsoft.UI.Xaml.Thickness(0);
        }

        if (platformView is Microsoft.UI.Xaml.Controls.TextBox textBox)
        {
            textBox.Resources["TextControlBorderThemeThickness"] = new Microsoft.UI.Xaml.Thickness(0);
            textBox.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
        }
#endif
#if ANDROID
        if (platformView is Android.Views.View androidView)
        {
            androidView.Background = null;
            androidView.SetBackgroundColor(Android.Graphics.Color.Transparent);
            if (androidView is Android.Widget.EditText editText)
            {
                editText.BackgroundTintList =
                    Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                editText.SetPadding(0, editText.PaddingTop, 0, editText.PaddingBottom);
            }
        }
#endif
#if IOS || MACCATALYST
        if (platformView is UIKit.UITextField field)
            field.BorderStyle = UIKit.UITextBorderStyle.None;
        if (platformView is UIKit.UITextView textView)
        {
            textView.BackgroundColor = UIKit.UIColor.Clear;
            textView.Layer.BorderWidth = 0;
        }
        if (platformView is UIKit.UIView uiView)
            uiView.BackgroundColor = UIKit.UIColor.Clear;
#endif
    }
}
