using Microsoft.Maui.Handlers;

namespace AveroNova.App.UI.Helpers;

public static class NativeInputChrome
{
    public static void Register()
    {
        EntryHandler.Mapper.AppendToMapping("AveroNovaInputChrome", (handler, _) => Apply(handler.PlatformView));
        EditorHandler.Mapper.AppendToMapping("AveroNovaInputChrome", (handler, _) => Apply(handler.PlatformView));
        SearchBarHandler.Mapper.AppendToMapping("AveroNovaInputChrome", (handler, _) => Apply(handler.PlatformView));
        PickerHandler.Mapper.AppendToMapping("AveroNovaInputChrome", (handler, _) => Apply(handler.PlatformView));
        DatePickerHandler.Mapper.AppendToMapping("AveroNovaInputChrome", (handler, _) => Apply(handler.PlatformView));
        EntryHandler.Mapper.AppendToMapping("AveroNovaInputFocus", (handler, view) => AttachFocus(view as VisualElement));
        EditorHandler.Mapper.AppendToMapping("AveroNovaInputFocus", (handler, view) => AttachFocus(view as VisualElement));
        SearchBarHandler.Mapper.AppendToMapping("AveroNovaInputFocus", (handler, view) => AttachFocus(view as VisualElement));
        PickerHandler.Mapper.AppendToMapping("AveroNovaInputFocus", (handler, view) => AttachFocus(view as VisualElement));
        GlobalPointerCursor.Register();
    }

    private static void AttachFocus(VisualElement? element)
    {
        if (element is not InputView input) return;
        input.Focused -= OnInputFocused; input.Unfocused -= OnInputUnfocused;
        input.Focused += OnInputFocused; input.Unfocused += OnInputUnfocused;
    }
    private static void OnInputFocused(object? sender, FocusEventArgs e) => SetContainerFocus(sender as View, true);
    private static void OnInputUnfocused(object? sender, FocusEventArgs e) => SetContainerFocus(sender as View, false);
    private static void SetContainerFocus(View? view, bool focused)
    {
        var border = FindInputBorder(view); if (border is null) return;
        border.Stroke = Colors.Transparent; border.StrokeThickness = 0;
    }
    private static Border? FindInputBorder(Element? element)
    {
        while (element is not null) { if (element is Border border) return border; element = element.Parent; }
        return null;
    }

    private static void Apply(object? platformView)
    {
        if (platformView is null) return;
#if WINDOWS
        if (platformView is Microsoft.UI.Xaml.Controls.Control control)
        {
            control.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            control.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            control.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            control.FocusVisualPrimaryThickness = new Microsoft.UI.Xaml.Thickness(0);
            control.FocusVisualSecondaryThickness = new Microsoft.UI.Xaml.Thickness(0);
            control.UseSystemFocusVisuals = false;
        }
        if (platformView is Microsoft.UI.Xaml.Controls.TextBox textBox)
        {
            textBox.Padding = new Microsoft.UI.Xaml.Thickness(0);
            textBox.VerticalContentAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center;
            textBox.Resources["TextControlBorderThemeThickness"] = new Microsoft.UI.Xaml.Thickness(0);
            textBox.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
        }
        if (platformView is Microsoft.UI.Xaml.Controls.ComboBox combo)
        {
            // Vertically center selected value and remove WinUI border chrome.
            // Use small symmetric vertical padding (4px top/bottom) so the
            // ContentPresenter inside the ComboBox header is never clipped.
            combo.Padding = new Microsoft.UI.Xaml.Thickness(12, 4, 12, 4);
            combo.VerticalContentAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center;
            combo.HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;
            combo.MinHeight = 40;
            combo.Height = double.NaN; // let MAUI Border control height

            // Override WinUI theme resources for selected-item presenter
            combo.Resources["ComboBoxMinHeight"] = 40d;
            combo.Resources["ComboBoxPadding"] = new Microsoft.UI.Xaml.Thickness(12, 4, 12, 4);
            combo.Resources["ComboBoxEditableTextPadding"] = new Microsoft.UI.Xaml.Thickness(12, 4, 12, 4);
            combo.Resources["ComboBoxTextBoxVerticalAlignment"] = Microsoft.UI.Xaml.VerticalAlignment.Center;

            // Dropdown list items: vertically centered, sufficient padding
            combo.Resources["ComboBoxItemMinHeight"] = 36d;
            combo.Resources["ComboBoxItemPadding"] = new Microsoft.UI.Xaml.Thickness(12, 6, 12, 6);
            combo.Resources["ComboBoxItemHorizontalContentAlignment"] = Microsoft.UI.Xaml.HorizontalAlignment.Left;
            combo.Resources["ComboBoxItemVerticalContentAlignment"] = Microsoft.UI.Xaml.VerticalAlignment.Center;

            // Walk the visual tree after layout to enforce center alignment on the
            // ContentPresenter that renders the selected item text.
            combo.Loaded += (s, _) => AlignComboBoxContent(s as Microsoft.UI.Xaml.Controls.ComboBox);
            combo.SelectionChanged += (s, _) => AlignComboBoxContent(s as Microsoft.UI.Xaml.Controls.ComboBox);
        }
#endif
#if ANDROID
        if (platformView is Android.Views.View androidView)
        {
            androidView.Background = null;
            androidView.SetBackgroundColor(Android.Graphics.Color.Transparent);
            if (androidView is Android.Widget.EditText editText)
            {
                editText.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                editText.Gravity = Android.Views.GravityFlags.CenterVertical;
            }
        }
#endif
#if IOS || MACCATALYST
        if (platformView is UIKit.UITextField field) field.BorderStyle = UIKit.UITextBorderStyle.None;
        if (platformView is UIKit.UITextView textView) { textView.BackgroundColor = UIKit.UIColor.Clear; textView.Layer.BorderWidth = 0; }
        if (platformView is UIKit.UIView uiView) uiView.BackgroundColor = UIKit.UIColor.Clear;
#endif
    }

#if WINDOWS
    /// <summary>
    /// Walks the WinUI visual tree of a ComboBox and forces VerticalAlignment=Center
    /// on the ContentPresenter that hosts the selected-item text, preventing clipping.
    /// </summary>
    private static void AlignComboBoxContent(Microsoft.UI.Xaml.Controls.ComboBox? combo)
    {
        if (combo is null) return;
        try
        {
            var presenter = FindVisualChild<Microsoft.UI.Xaml.Controls.ContentPresenter>(combo);
            if (presenter is not null)
            {
                presenter.VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center;
                presenter.VerticalContentAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center;
            }
        }
        catch { /* ignore layout-phase exceptions */ }
    }

    private static T? FindVisualChild<T>(Microsoft.UI.Xaml.DependencyObject parent)
        where T : Microsoft.UI.Xaml.DependencyObject
    {
        if (parent is null) return null;
        int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var result = FindVisualChild<T>(child);
            if (result is not null) return result;
        }
        return null;
    }
#endif
}
