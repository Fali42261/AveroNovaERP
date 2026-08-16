using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Handlers;

namespace AveroNova.App.UI.Helpers;

/// <summary>
/// Windows-only: apply the OS Hand/Pointer cursor to clickable controls automatically.
/// Does not change touch behavior on mobile.
/// </summary>
public static class GlobalPointerCursor
{
    private static readonly ConditionalWeakTable<BindableObject, Tracker> Trackers = new();

    public static void Register()
    {
#if WINDOWS
        ButtonHandler.Mapper.AppendToMapping("AveroNovaPointerCursor", (handler, view) =>
            TrackAndApply(view as VisualElement, alwaysHand: true));
        ImageButtonHandler.Mapper.AppendToMapping("AveroNovaPointerCursor", (handler, view) =>
            TrackAndApply(view as VisualElement, alwaysHand: true));
        RadioButtonHandler.Mapper.AppendToMapping("AveroNovaPointerCursor", (handler, view) =>
            TrackAndApply(view as VisualElement, alwaysHand: true));
        CheckBoxHandler.Mapper.AppendToMapping("AveroNovaPointerCursor", (handler, view) =>
            TrackAndApply(view as VisualElement, alwaysHand: true));
        SwitchHandler.Mapper.AppendToMapping("AveroNovaPointerCursor", (handler, view) =>
            TrackAndApply(view as VisualElement, alwaysHand: true));

        ViewHandler.ViewMapper.AppendToMapping("AveroNovaPointerCursorGestures", (handler, view) =>
        {
            if (view is VisualElement visual)
                TrackAndApply(visual, alwaysHand: false);
        });
#endif
    }

    private static void TrackAndApply(VisualElement? element, bool alwaysHand)
    {
        if (element is null)
            return;

        var tracker = Trackers.GetValue(element, static key => new Tracker((VisualElement)key));
        tracker.AlwaysHand = alwaysHand || tracker.AlwaysHand;
        tracker.Apply();
    }

    private sealed class Tracker
    {
        private readonly VisualElement _element;

        public Tracker(VisualElement element)
        {
            _element = element;
            _element.HandlerChanged += (_, _) => Apply();
            _element.Loaded += (_, _) => Apply();
            _element.PropertyChanged += OnPropertyChanged;

            if (_element is View view && view.GestureRecognizers is INotifyCollectionChanged gestures)
                gestures.CollectionChanged += (_, _) => Apply();
        }

        public bool AlwaysHand { get; set; }

        public void Apply()
        {
            if (!ShouldUseHand(_element, AlwaysHand))
                return;

            CursorBehavior.SetCursor(_element, CursorType.Hand);
            CursorBehavior.ApplyCursor(_element, CursorType.Hand);
        }

        private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(VisualElement.IsEnabled) or nameof(VisualElement.Handler))
                Apply();
        }
    }

    private static bool ShouldUseHand(VisualElement element, bool alwaysHand)
    {
        if (!element.IsEnabled)
            return false;

        if (alwaysHand)
            return true;

        if (element is Button or ImageButton or RadioButton or CheckBox or Switch)
            return true;

        if (element is View view && HasTapOrClickGesture(view.GestureRecognizers))
            return true;

        if (element is Label label && label.FormattedText?.Spans is not null)
        {
            foreach (var span in label.FormattedText.Spans)
            {
                if (HasTapOrClickGesture(span.GestureRecognizers))
                    return true;
            }
        }

        return false;
    }

    private static bool HasTapOrClickGesture(IEnumerable<IGestureRecognizer>? recognizers)
    {
        if (recognizers is null)
            return false;

        foreach (var recognizer in recognizers)
        {
            if (recognizer is TapGestureRecognizer)
                return true;
        }

        return false;
    }
}
