using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Models;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Views.Layout;

public enum HeaderAccountMenuChoice
{
    Logout,
    Settings,
    Administrator
}

public sealed class HeaderAccountMenuModel
{
    public bool CanSettings { get; init; }

    public bool CanAdministrator { get; init; }

    public bool CanTheme { get; init; } = true;

    public ThemeMode CurrentTheme { get; init; }
}

/// <summary>
/// Header user-icon dropdown. Presentation only — permissions come from the
/// existing catalog/access snapshot. Theme changes go through ISettingsService.
/// </summary>
public sealed class HeaderAccountMenuOverlay : Grid
{
    private const double DesktopWidth = 240;
    private const double MobileWidth = 260;

    private readonly BoxView _scrim;
    private readonly Border _panel;
    private readonly VerticalStackLayout _host;
    private readonly ScrollView _scroll;

    private HeaderAccountMenuModel _model = new();
    private bool _themePane;
    private bool _compact;

#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _escapeTarget;
    private Microsoft.UI.Xaml.Input.KeyEventHandler? _escapeHandler;
#endif

    public event EventHandler<HeaderAccountMenuChoice>? ChoiceMade;
    public event EventHandler<ThemeMode>? ThemeChosen;
    public event EventHandler? Closed;

    public HeaderAccountMenuOverlay()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        ZIndex = 3000;
        IsVisible = false;
        AutomationId = "HeaderAccountMenu";
        SemanticProperties.SetDescription(this, "Account menu");

        _scrim = new BoxView
        {
            Color = Color.FromArgb("#01000000"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        SemanticProperties.SetDescription(_scrim, "Close account menu");
        var scrimTap = new TapGestureRecognizer();
        scrimTap.Tapped += (_, _) => Close();
        _scrim.GestureRecognizers.Add(scrimTap);

        _host = new VerticalStackLayout
        {
            Spacing = 2
        };

        _scroll = new ScrollView
        {
            Orientation = ScrollOrientation.Vertical,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            VerticalScrollBarVisibility = ScrollBarVisibility.Default,
            Content = _host
        };

        _panel = new Border
        {
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
            Padding = new Thickness(6),
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            WidthRequest = DesktopWidth,
            Content = _scroll,
            Shadow = new Shadow
            {
                Brush = Color.FromArgb("#111827"),
                Opacity = 0.16f,
                Radius = 16,
                Offset = new Point(0, 6)
            }
        };
        ApplyPanelChrome();

        Children.Add(_scrim);
        Children.Add(_panel);

        SizeChanged += (_, _) =>
        {
            if (IsOpen)
                PositionPanel();
        };
    }

    public bool IsOpen => IsVisible;

    public void Open(HeaderAccountMenuModel model, bool compact)
    {
        _model = model ?? new HeaderAccountMenuModel();
        _compact = compact;
        _themePane = false;
        Rebuild();
        PositionPanel();
        IsVisible = true;
        AttachEscape();
        _ = _panel.FadeTo(1, 90, Easing.CubicOut);
    }

    public void Close()
    {
        if (!IsVisible)
        {
            DetachEscape();
            return;
        }

        IsVisible = false;
        _themePane = false;
        DetachEscape();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void Rebuild()
    {
        _host.Children.Clear();
        if (_themePane)
        {
            _host.Children.Add(BuildItem(
                "IconBack",
                "Appearance",
                submenu: false,
                selected: false,
                onTap: () =>
                {
                    _themePane = false;
                    Rebuild();
                    PositionPanel();
                }));
            _host.Children.Add(BuildItem(
                "IconLightMode",
                "Light",
                submenu: false,
                selected: _model.CurrentTheme == ThemeMode.Light,
                onTap: () => ChooseTheme(ThemeMode.Light)));
            _host.Children.Add(BuildItem(
                "IconDarkMode",
                "Dark",
                submenu: false,
                selected: _model.CurrentTheme == ThemeMode.Dark,
                onTap: () => ChooseTheme(ThemeMode.Dark)));
            _host.Children.Add(BuildItem(
                "IconSystemMode",
                "System Default",
                submenu: false,
                selected: _model.CurrentTheme == ThemeMode.System,
                onTap: () => ChooseTheme(ThemeMode.System)));
            return;
        }

        // Order is required: Settings, Administrator, Theme, then Logout last.
        if (_model.CanSettings)
        {
            _host.Children.Add(BuildItem(
                "IconSettings",
                "Settings",
                submenu: false,
                selected: false,
                onTap: () => Choose(HeaderAccountMenuChoice.Settings)));
        }

        if (_model.CanAdministrator)
        {
            _host.Children.Add(BuildItem(
                "IconAdministration",
                "Administrator",
                submenu: false,
                selected: false,
                onTap: () => Choose(HeaderAccountMenuChoice.Administrator)));
        }

        if (_model.CanTheme)
        {
            _host.Children.Add(BuildItem(
                "IconTheme",
                "Theme",
                submenu: true,
                selected: false,
                onTap: () =>
                {
                    _themePane = true;
                    Rebuild();
                    PositionPanel();
                }));
        }

        if (_host.Children.Count > 0)
            _host.Children.Add(BuildSeparator());

        _host.Children.Add(BuildItem(
            "IconLogout",
            "Logout",
            submenu: false,
            selected: false,
            onTap: () => Choose(HeaderAccountMenuChoice.Logout)));
    }

    private View BuildSeparator()
    {
        var line = new BoxView
        {
            HeightRequest = 1,
            HorizontalOptions = LayoutOptions.Fill,
            Margin = new Thickness(8, 6)
        };
        SemanticProperties.SetDescription(line, "Logout separator");
        line.SetAppThemeColor(
            BoxView.ColorProperty,
            Res("BorderColor", "#E2E8F0"),
            Res("BorderColorDark", "#334155"));
        return line;
    }

    private View BuildItem(string iconKey, string label, bool submenu, bool selected, Action onTap)
    {
        var height = _compact ? 44 : 40;
        var root = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(6) },
            Padding = new Thickness(12, 0),
            HeightRequest = height,
            MinimumHeightRequest = height,
            BackgroundColor = Colors.Transparent
        };
        SemanticProperties.SetDescription(root, label);
        SemanticProperties.SetHint(root, submenu ? "Opens submenu" : "Activate");

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(20),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 10,
            VerticalOptions = LayoutOptions.Fill
        };

        var icon = new Label
        {
            Text = SidebarTokens.Glyph(iconKey),
            FontFamily = "OpenSansRegular",
            FontSize = 15,
            VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        ApplyTextColor(icon, selected);

        var text = new Label
        {
            Text = label,
            FontFamily = "OpenSansSemibold",
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        };
        ApplyTextColor(text, selected);
        text.SetValue(Grid.ColumnProperty, 1);

        var trailing = new Label
        {
            FontFamily = "OpenSansRegular",
            FontSize = 13,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };
        trailing.SetValue(Grid.ColumnProperty, 2);
        if (submenu)
        {
            trailing.Text = SidebarTokens.Glyph("IconChevronRight", "›");
            ApplyMutedColor(trailing);
        }
        else if (selected)
        {
            trailing.Text = SidebarTokens.Glyph("IconCheck", "✓");
            ApplyTextColor(trailing, selected: true);
        }
        else
        {
            trailing.IsVisible = false;
        }

        grid.Children.Add(icon);
        grid.Children.Add(text);
        grid.Children.Add(trailing);
        root.Content = grid;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => onTap();
        root.GestureRecognizers.Add(tap);

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => ApplyItemChrome(root, hovered: true, focused: false, selected);
        pointer.PointerExited += (_, _) => ApplyItemChrome(root, hovered: false, focused: false, selected);
        root.GestureRecognizers.Add(pointer);

        ApplyItemChrome(root, hovered: false, focused: false, selected);
        EnableKeyboard(root, onTap);
        return root;
    }

    private void Choose(HeaderAccountMenuChoice choice)
    {
        Close();
        ChoiceMade?.Invoke(this, choice);
    }

    private void ChooseTheme(ThemeMode mode)
    {
        ThemeChosen?.Invoke(this, mode);
        Close();
    }

    private void PositionPanel()
    {
        var width = _compact ? MobileWidth : DesktopWidth;
        var available = Width > 0 ? Width : width;
        width = Math.Min(width, Math.Max(200, available - 16));
        _panel.WidthRequest = width;

        var top = _compact ? 62 : 70;
        var side = _compact ? 8 : 16;
        var maxHeight = Height > top + 24 ? Height - top - 16 : 240;
        _panel.MaximumHeightRequest = Math.Max(120, maxHeight);
        _panel.Margin = new Thickness(side, top, side, 16);
        ApplyPanelChrome();
    }

    private void ApplyPanelChrome()
    {
        _panel.SetAppThemeColor(
            VisualElement.BackgroundColorProperty,
            Res("SurfaceColor", "#FFFFFF"),
            Res("SurfaceColorDark", "#1E293B"));
        _panel.SetAppTheme(
            Border.StrokeProperty,
            new SolidColorBrush(Res("BorderColor", "#E2E8F0")),
            new SolidColorBrush(Res("BorderColorDark", "#334155")));
    }

    private static void ApplyItemChrome(Border border, bool hovered, bool focused, bool selected)
    {
        var hover = SidebarTokens.Color("Gray100", "Gray800");
        border.BackgroundColor = hovered || selected
            ? hover
            : Colors.Transparent;
        border.StrokeThickness = focused ? 1 : 0;
        border.Stroke = focused
            ? SidebarTokens.Color("PrimaryColor", "Primary300")
            : Colors.Transparent;
    }

    private static void ApplyTextColor(Label label, bool selected)
    {
        label.SetAppThemeColor(
            Label.TextColorProperty,
            selected ? Res("PrimaryColor", "#2563EB") : Res("TextPrimary", "#0F172A"),
            selected ? Res("Primary300", "#93C5FD") : Res("TextPrimaryDark", "#F1F5F9"));
    }

    private static void ApplyMutedColor(Label label)
    {
        label.SetAppThemeColor(
            Label.TextColorProperty,
            Res("TextSecondary", "#64748B"),
            Res("TextSecondaryDark", "#94A3B8"));
    }

    private static Color Res(string key, string fallback)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is Color color)
            return color;
        return Color.FromArgb(fallback);
    }

    private void EnableKeyboard(Border root, Action onTap)
    {
#if WINDOWS
        root.HandlerChanged += (_, _) =>
        {
            if (root.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.Control native)
                return;

            native.IsTabStop = true;
            native.UseSystemFocusVisuals = false;
            native.GotFocus += (_, _) => ApplyItemChrome(root, hovered: false, focused: true, selected: false);
            native.LostFocus += (_, _) => ApplyItemChrome(root, hovered: false, focused: false, selected: false);
            native.KeyDown += (_, e) =>
            {
                if (e.Key is not Windows.System.VirtualKey.Enter and not Windows.System.VirtualKey.Space)
                    return;
                onTap();
                e.Handled = true;
            };
        };
#else
        _ = root;
        _ = onTap;
#endif
    }

    private void AttachEscape()
    {
#if WINDOWS
        DetachEscape();
        if (Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window native
            && native.Content is Microsoft.UI.Xaml.UIElement content)
        {
            _escapeTarget = content;
            _escapeHandler = OnNativeEscape;
            content.KeyDown += _escapeHandler;
        }
#endif
    }

    private void DetachEscape()
    {
#if WINDOWS
        if (_escapeTarget != null && _escapeHandler != null)
            _escapeTarget.KeyDown -= _escapeHandler;
        _escapeTarget = null;
        _escapeHandler = null;
#endif
    }

#if WINDOWS
    private void OnNativeEscape(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Escape || !IsOpen)
            return;
        Close();
        e.Handled = true;
    }
#endif

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler == null)
            DetachEscape();
    }
}
