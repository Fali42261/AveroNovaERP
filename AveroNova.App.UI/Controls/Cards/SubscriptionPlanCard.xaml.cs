using System.ComponentModel;
using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using CheckPath = Microsoft.Maui.Controls.Shapes.Path;

namespace AveroNova.App.UI.Controls.Cards;

public partial class SubscriptionPlanCard : ContentView
{
    public const double FeatureRowHeight = 28;
    public const double DesktopBenefitsMaxHeight = 400;

    private RegisterPlanOption? _plan;
    private bool _hovering;
    private bool _compactBenefits = true;
    private int _alignedFeatureCount;
    private readonly List<CheckPath> _checkMarks = [];

    public SubscriptionPlanCard()
    {
        InitializeComponent();
        BindingContextChanged += OnBindingContextChanged;
        Unloaded += OnUnloaded;
        Loaded += OnLoaded;
        BenefitsList.SizeChanged += OnBenefitsListSizeChanged;
    }

    public int FeatureCount => _plan?.Features.Count
        ?? (BindingContext as RegisterPlanOption)?.Features.Count
        ?? 0;

    public void ApplyBenefitsLayout(bool compact, int alignedFeatureCount = 0)
    {
        _compactBenefits = compact;
        _alignedFeatureCount = alignedFeatureCount;
        RenderBenefits();
        ApplyBenefitsViewport();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app is not null)
            app.RequestedThemeChanged += OnRequestedThemeChanged;
        RenderBenefits();
        ApplyBenefitsViewport();
        ApplyChrome();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        BenefitsList.SizeChanged -= OnBenefitsListSizeChanged;
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app is not null)
            app.RequestedThemeChanged -= OnRequestedThemeChanged;
        DetachPlan();
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
        => ApplyChrome();

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        DetachPlan();
        _plan = BindingContext as RegisterPlanOption;
        if (_plan is not null)
            _plan.PropertyChanged += OnPlanPropertyChanged;
        RenderBenefits();
        ApplyBenefitsViewport();
        ApplyChrome();
    }

    private void DetachPlan()
    {
        if (_plan is not null)
            _plan.PropertyChanged -= OnPlanPropertyChanged;
        _plan = null;
    }

    private void OnPlanPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RegisterPlanOption.IsSelected)
            or nameof(RegisterPlanOption.IsComingSoon)
            or nameof(RegisterPlanOption.IsAvailable))
        {
            ApplyChrome();
        }
    }

    private void RenderBenefits()
    {
        if (BenefitsList is null)
            return;

        var plan = _plan ?? BindingContext as RegisterPlanOption;
        BenefitsList.Children.Clear();
        _checkMarks.Clear();
        if (plan is null)
            return;

        foreach (var feature in plan.Features)
        {
            if (string.IsNullOrWhiteSpace(feature.Text))
                continue;
            BenefitsList.Children.Add(CreateFeatureRow(feature.Text, plan.IsComingSoon));
        }
    }

    private View CreateFeatureRow(string text, bool planned)
    {
        var check = new CheckPath
        {
            Data = CreateCheckGeometry(),
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Aspect = Stretch.Uniform,
            WidthRequest = 14,
            HeightRequest = 14,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 3, 0, 0),
            InputTransparent = true,
            Stroke = new SolidColorBrush(CheckColor(planned))
        };
        _checkMarks.Add(check);

        var label = new Label
        {
            Text = text,
            LineBreakMode = LineBreakMode.WordWrap,
            FontSize = 13,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true
        };
        if (GetStyle("PlanCardFeatureText") is Style featureStyle)
            label.Style = featureStyle;

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(new GridLength(18)),
                new(GridLength.Star)
            },
            ColumnSpacing = 10,
            Margin = new Thickness(0, 0, 0, 8),
            InputTransparent = true
        };
        row.Add(check);
        row.Add(label, 1);
        return row;
    }

    private static PathGeometry CreateCheckGeometry()
    {
        var figure = new PathFigure
        {
            StartPoint = new Point(1.2, 7.2),
            IsClosed = false
        };
        figure.Segments.Add(new LineSegment(new Point(5.2, 11.2)));
        figure.Segments.Add(new LineSegment(new Point(12.8, 2.4)));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private void ApplyBenefitsViewport()
    {
        if (BenefitsViewport is null || BenefitsScroll is null)
            return;

        var count = Math.Max(FeatureCount, 1);
        var contentHeight = count * FeatureRowHeight;
        if (_compactBenefits)
        {
            BenefitsViewport.HeightRequest = contentHeight;
            BenefitsViewport.MinimumHeightRequest = contentHeight;
            BenefitsViewport.MaximumHeightRequest = double.PositiveInfinity;
            BenefitsScroll.IsEnabled = false;
            BenefitsScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Never;
            return;
        }

        var alignedHeight = Math.Max(_alignedFeatureCount, count) * FeatureRowHeight;
        var viewport = Math.Min(Math.Max(alignedHeight, contentHeight), DesktopBenefitsMaxHeight);
        BenefitsViewport.HeightRequest = viewport;
        BenefitsViewport.MinimumHeightRequest = viewport;
        BenefitsViewport.MaximumHeightRequest = viewport;
        var enableScroll = contentHeight > viewport + 1;
        BenefitsScroll.IsEnabled = enableScroll;
        BenefitsScroll.VerticalScrollBarVisibility = enableScroll
            ? ScrollBarVisibility.Default
            : ScrollBarVisibility.Never;
    }

    private void OnBenefitsListSizeChanged(object? sender, EventArgs e)
    {
        if (BenefitsList.Height <= 1 || BenefitsViewport is null || BenefitsScroll is null)
            return;

        if (_compactBenefits)
        {
            BenefitsViewport.HeightRequest = BenefitsList.Height;
            BenefitsViewport.MinimumHeightRequest = BenefitsList.Height;
            BenefitsViewport.MaximumHeightRequest = double.PositiveInfinity;
            return;
        }

        if (BenefitsList.Height > BenefitsViewport.HeightRequest + 1)
        {
            BenefitsScroll.IsEnabled = true;
            BenefitsScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Default;
        }
    }

    private void OnCardTapped(object? sender, TappedEventArgs e)
        => RequestAction();

    private void OnActionClicked(object? sender, EventArgs e)
        => RequestAction();

    private void OnPointerEntered(object? sender, PointerEventArgs e)
        => SetHover(true);

    private void OnPointerExited(object? sender, PointerEventArgs e)
        => SetHover(false);

    private void RequestAction()
    {
        if (_plan is not { IsSelectable: true })
            return;

        for (Element? current = Parent; current is not null; current = current.Parent)
        {
            if (current.BindingContext is RegisterViewModel vm)
            {
                vm.SelectPlanCommand.Execute(_plan.Id);
                return;
            }
        }
    }

    private void SetHover(bool hovering)
    {
        _hovering = hovering && _plan is { IsSelectable: true };
        ApplyChrome();
    }

    private void ApplyChrome()
    {
        var plan = _plan ?? BindingContext as RegisterPlanOption;
        var selected = plan?.IsSelected == true;
        var locked = plan?.IsLocked == true;
        var dark = IsDarkTheme();

        CardBorder.StrokeThickness = selected || (_hovering && !locked) ? 2 : 1;
        CardBorder.Stroke = new SolidColorBrush(
            selected || (_hovering && !locked)
                ? Res("PrimaryColor", "#2563EB")
                : Res(dark ? "BorderColorDark" : "BorderColor", dark ? "#334155" : "#E2E8F0"));
        CardBorder.BackgroundColor = selected
            ? Res(dark ? "PlanSelectedFillDark" : "PlanSelectedFill", dark ? "#172554" : "#EFF6FF")
            : Res(dark ? "CardBackgroundDark" : "CardBackground", dark ? "#1E293B" : "#FFFFFF");

        BadgeBorder.BackgroundColor = selected
            ? Res(dark ? "Primary800" : "Primary100", dark ? "#1E40AF" : "#DBEAFE")
            : Res(dark ? "Gray800" : "Gray100", dark ? "#1F2937" : "#F3F4F6");
        BadgeLabel.TextColor = selected
            ? Res(dark ? "Primary200" : "PrimaryColor", dark ? "#BFDBFE" : "#2563EB")
            : Res(dark ? "TextSecondaryDark" : "TextSecondary", dark ? "#94A3B8" : "#64748B");

        LockedActionBorder.BackgroundColor = Res(
            dark ? "Gray800" : "Gray100",
            dark ? "#1F2937" : "#F3F4F6");
        LockedActionBorder.Stroke = new SolidColorBrush(
            Res(dark ? "BorderColorDark" : "BorderColor", dark ? "#334155" : "#E2E8F0"));
        var lockedText = Res(dark ? "TextSecondaryDark" : "TextSecondary", dark ? "#94A3B8" : "#64748B");
        LockedActionIcon.TextColor = lockedText;
        LockedActionLabel.TextColor = lockedText;

        var checkBrush = new SolidColorBrush(CheckColor(plan?.IsComingSoon == true));
        foreach (var mark in _checkMarks)
            mark.Stroke = checkBrush;

        CursorBehavior.SetCursor(CardBorder, locked ? CursorType.Arrow : CursorType.Hand);
        CursorBehavior.SetCursor(this, locked ? CursorType.Arrow : CursorType.Hand);
    }

    private Color CheckColor(bool planned)
        => planned
            ? Res(IsDarkTheme() ? "TextSecondaryDark" : "TextSecondary", IsDarkTheme() ? "#94A3B8" : "#64748B")
            : Res("PrimaryColor", "#2563EB");

    private static Style? GetStyle(string key)
        => Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true
            ? value as Style
            : null;

    private static bool IsDarkTheme()
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app is null)
            return false;

        var theme = app.UserAppTheme == AppTheme.Unspecified
            ? app.RequestedTheme
            : app.UserAppTheme;
        return theme == AppTheme.Dark;
    }

    private static Color Res(string key, string fallback)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is Color color)
        {
            return color;
        }

        return Color.FromArgb(fallback);
    }
}
