using AveroNova.App.UI.Layout;
using AveroNova.Application.Navigation;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Views.Layout;

public partial class AppSidebarView : ContentView
{
    public static readonly BindableProperty SelectedKeyProperty =
        BindableProperty.Create(
            nameof(SelectedKey),
            typeof(string),
            typeof(AppSidebarView),
            null,
            propertyChanged: OnSelectedKeyChanged);

    public static readonly BindableProperty DensityProperty =
        BindableProperty.Create(
            nameof(Density),
            typeof(SidebarDensity),
            typeof(AppSidebarView),
            SidebarDensity.Desktop,
            propertyChanged: OnDensityChanged);

    public static readonly BindableProperty IsCollapsedProperty =
        BindableProperty.Create(
            nameof(IsCollapsed),
            typeof(bool),
            typeof(AppSidebarView),
            false,
            propertyChanged: OnCollapsedChanged);

    private IReadOnlyList<NavigationMenuNode> _items = [];
    private string? _expandedGroup;
    private readonly Dictionary<string, SidebarItemVisual> _visuals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VerticalStackLayout> _childHosts = new(StringComparer.OrdinalIgnoreCase);
    private int _animationSerial;

    public event EventHandler<string>? MenuSelected;
    public event EventHandler? ExpandRequested;

    public AppSidebarView()
    {
        InitializeComponent();
        SizeChanged += OnSidebarSizeChanged;
        BrandHeader.SizeChanged += OnSidebarSizeChanged;
        NavScroll.HandlerChanged += OnNavScrollHandlerChanged;
        if (Microsoft.Maui.Controls.Application.Current != null)
            Microsoft.Maui.Controls.Application.Current.RequestedThemeChanged += OnThemeChanged;
    }

    public string? SelectedKey
    {
        get => (string?)GetValue(SelectedKeyProperty);
        set => SetValue(SelectedKeyProperty, value);
    }

    public SidebarDensity Density
    {
        get => (SidebarDensity)GetValue(DensityProperty);
        set => SetValue(DensityProperty, value);
    }

    public bool IsCollapsed
    {
        get => (bool)GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    public void BindMenus(IReadOnlyList<NavigationMenuNode> items, string? selectedKey = null)
    {
        _items = items ?? [];
        if (!string.IsNullOrWhiteSpace(selectedKey))
            SelectedKey = selectedKey;

        _expandedGroup = FindParentKey(SelectedKey);
        Rebuild();
    }

    private static void OnSelectedKeyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (AppSidebarView)bindable;
        view._expandedGroup = view.FindParentKey(newValue as string);
        view.ApplyExpandState(animate: false);
        view.ApplySelection();
    }

    private static void OnDensityChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (AppSidebarView)bindable;
        if (view._items.Count == 0)
        {
            view.ApplyBrandDensity();
            return;
        }
        view.Rebuild();
    }

    private static void OnCollapsedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (AppSidebarView)bindable;
        if (view._items.Count == 0)
        {
            view.ApplyBrandDensity();
            return;
        }
        view.Rebuild();
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e)
        => MainThread.BeginInvokeOnMainThread(Rebuild);

    private void Rebuild()
    {
        MenuHost.Children.Clear();
        _visuals.Clear();
        _childHosts.Clear();

        ApplyBrandDensity();

        string? lastSection = null;
        var firstSection = true;
        foreach (var item in _items)
        {
            var section = SectionTitle(item);
            if (!IsCollapsed
                && !string.IsNullOrWhiteSpace(section)
                && !string.Equals(section, lastSection, StringComparison.Ordinal))
            {
                MenuHost.Children.Add(BuildSection(section, firstSection));
                firstSection = false;
                lastSection = section;
            }

            MenuHost.Children.Add(BuildItem(item, isChild: false));

            if (!item.IsAccordion)
                continue;

            var open = IsExpanded(item.Key) && !IsCollapsed;
            var host = new VerticalStackLayout
            {
                Spacing = 2,
                IsVisible = open,
                Opacity = open ? 1 : 0
            };
            foreach (var child in item.Children)
                host.Children.Add(BuildItem(child, isChild: true));

            _childHosts[item.Key] = host;
            MenuHost.Children.Add(host);
        }

        ApplySelection();
        ApplyExpandState(animate: false);
        ApplyNavViewport();
    }

    public bool KeepsDrawerOpen(string key)
        => _items.Any(item =>
            item.IsAccordion
            && item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    private static string? SectionTitle(NavigationMenuNode item)
    {
        var section = item.GroupLabel;
        if (string.IsNullOrWhiteSpace(section))
            section = SidebarSectionMap.ForKey(item.Key);
        return string.IsNullOrWhiteSpace(section) ? null : section.ToUpperInvariant();
    }

    private void ApplyBrandDensity()
    {
        var collapsed = IsCollapsed;
        var tablet = Density == SidebarDensity.Tablet;
        var logo = collapsed ? 42 : tablet ? 40 : 42;
        BrandLogo.WidthRequest = logo;
        BrandLogo.HeightRequest = logo;
        BrandLogo.HorizontalOptions = collapsed ? LayoutOptions.Center : LayoutOptions.Start;
        BrandText.IsVisible = !collapsed;
        BrandHeader.ColumnSpacing = collapsed ? 0 : 10;
        BrandHeader.Padding = collapsed
            ? new Thickness(11, 11, 11, 11)
            : tablet
                ? new Thickness(12, 12, 12, 10)
                : new Thickness(16, 14, 16, 12);
        BrandHeader.ColumnDefinitions = new ColumnDefinitionCollection(
            new ColumnDefinition(logo),
            new ColumnDefinition(GridLength.Star));
        MenuHost.Padding = collapsed
            ? new Thickness(8, 8, 8, 16)
            : tablet
                ? new Thickness(6, 8, 6, 16)
                : new Thickness(8, 8, 8, 16);
    }

    private View BuildSection(string title, bool first)
    {
        var label = new Label
        {
            Text = title,
            Style = SidebarTokens.Style("SidebarSection") ?? SidebarTokens.Style("SidebarGroupLabel")
        };
        var top = first ? 4 : SidebarTokens.Number("SidebarSectionGap", 12);
        label.Margin = new Thickness(8, top, 8, 6);
        SemanticProperties.SetHeadingLevel(label, SemanticHeadingLevel.Level2);
        return label;
    }

    private View BuildItem(NavigationMenuNode item, bool isChild)
    {
        var compact = Density == SidebarDensity.Tablet;
        var mobile = Density == SidebarDensity.Mobile;
        var height = isChild
            ? (mobile ? 44 : compact ? 38 : SidebarTokens.Number("SidebarChildItemHeight", 40))
            : (mobile ? 44 : compact ? 42 : SidebarTokens.Number("SidebarItemHeight", 44));
        var padH = compact ? 12 : mobile ? 16 : SidebarTokens.Number("SidebarItemPadH", 14);
        var radius = SidebarTokens.Number("SidebarItemRadius", 6);
        var iconSlot = SidebarTokens.Number("SidebarIconSlot", 20);
        var gap = SidebarTokens.Number("SidebarIconTextGap", 10);
        var chevronSize = SidebarTokens.Number("SidebarChevronSize", 14);

        var collapsed = IsCollapsed;
        var root = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(radius) },
            Padding = collapsed
                ? new Thickness(8, 0)
                : new Thickness(isChild ? padH + 12 : padH, 0, 12, 0),
            HeightRequest = height,
            MinimumHeightRequest = height,
            BackgroundColor = Colors.Transparent
        };
        SemanticProperties.SetDescription(root, item.Label);
        SemanticProperties.SetHint(root, item.IsAccordion ? "Expands section" : "Navigate");

        var grid = new Grid
        {
            ColumnDefinitions = collapsed
                ? new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star))
                : new ColumnDefinitionCollection(
                    new ColumnDefinition(iconSlot),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = collapsed ? 0 : gap,
            VerticalOptions = LayoutOptions.Fill
        };

        var icon = new Label
        {
            Text = SidebarTokens.Glyph(item.IconResourceKey),
            Style = SidebarTokens.Style("SidebarIcon"),
            WidthRequest = iconSlot,
            FontSize = SidebarTokens.Number("SidebarIconSize", 16),
            HorizontalOptions = collapsed ? LayoutOptions.Center : LayoutOptions.Start
        };
        icon.SetValue(Grid.ColumnProperty, 0);

        var label = new Label
        {
            Text = item.Label,
            Style = SidebarTokens.Style(isChild ? "SidebarChildItem" : "SidebarMenuItem"),
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
            HorizontalOptions = LayoutOptions.Fill,
            IsVisible = !collapsed
        };
        if (mobile)
            label.FontSize = isChild ? 14 : 15;
        label.SetValue(Grid.ColumnProperty, 1);

        var chevron = new Label
        {
            Text = SidebarTokens.Glyph("IconChevronRight", "›"),
            Style = SidebarTokens.Style("SidebarIcon"),
            FontSize = chevronSize,
            WidthRequest = item.IsAccordion ? chevronSize : 0,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End,
            IsVisible = item.IsAccordion && !collapsed,
            Rotation = IsExpanded(item.Key) ? 90 : 0,
            AnchorX = 0.5,
            AnchorY = 0.5
        };
        chevron.SetValue(Grid.ColumnProperty, 2);

        grid.Children.Add(icon);
        grid.Children.Add(label);
        grid.Children.Add(chevron);
        root.Content = grid;

        var visual = new SidebarItemVisual(item, root, icon, label, chevron, isChild);
        root.BindingContext = visual;
        _visuals[item.Key] = visual;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => OnItemTapped(item);
        root.GestureRecognizers.Add(tap);

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => SetHover(root, true);
        pointer.PointerExited += (_, _) => SetHover(root, false);
        root.GestureRecognizers.Add(pointer);

        root.HandlerChanged += (_, _) =>
        {
            ApplyItemChrome(root, IsSelected(item.Key), hovered: false, focused: false);
            EnableKeyboard(root, item);
        };

        ApplyItemChrome(root, IsSelected(item.Key), hovered: false, focused: false);
        return root;
    }

    private void OnItemTapped(NavigationMenuNode item)
    {
        if (item.IsAccordion)
        {
            if (IsCollapsed)
            {
                _expandedGroup = item.Key;
                ExpandRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (IsExpanded(item.Key))
            {
                var locked = item.Children.Any(child => IsSelected(child.Key));
                if (!locked)
                    _expandedGroup = null;
            }
            else
            {
                _expandedGroup = item.Key;
            }

            ApplyExpandState(animate: true);
            return;
        }

        _expandedGroup = FindParentKey(item.Key);
        ApplyExpandState(animate: true);
        SelectedKey = item.Key;
        MenuSelected?.Invoke(this, item.Key);
    }

    private void ApplyExpandState(bool animate)
    {
        var serial = ++_animationSerial;
        foreach (var pair in _childHosts)
        {
            var open = IsExpanded(pair.Key) && !IsCollapsed;
            var host = pair.Value;

            if (_visuals.TryGetValue(pair.Key, out var visual))
            {
                visual.Chevron.CancelAnimations();
                visual.Chevron.Text = SidebarTokens.Glyph("IconChevronRight", "›");
                if (animate)
                    _ = visual.Chevron.RotateTo(open ? 90 : 0, 140, Easing.CubicOut);
                else
                    visual.Chevron.Rotation = open ? 90 : 0;
            }

            if (!animate)
            {
                host.CancelAnimations();
                host.IsVisible = open;
                host.Opacity = open ? 1 : 0;
                continue;
            }

            _ = AnimateAccordionAsync(host, open, serial);
        }
    }

    private async Task AnimateAccordionAsync(VerticalStackLayout host, bool expand, int serial)
    {
        if (expand)
        {
            host.IsVisible = true;
            host.Opacity = 0;
            await host.FadeTo(1, 140, Easing.CubicOut);
        }
        else
        {
            await host.FadeTo(0, 110, Easing.CubicIn);
            if (serial == _animationSerial)
                host.IsVisible = false;
        }
    }

    private bool IsExpanded(string key)
        => !string.IsNullOrWhiteSpace(_expandedGroup)
           && _expandedGroup.Equals(key, StringComparison.OrdinalIgnoreCase);

    private bool IsSelected(string key)
        => !string.IsNullOrWhiteSpace(SelectedKey)
           && SelectedKey.Equals(key, StringComparison.OrdinalIgnoreCase);

    private void ApplySelection()
    {
        foreach (var visual in _visuals.Values)
            ApplyItemChrome(visual.Root, IsSelected(visual.Item.Key), hovered: false, focused: false);
    }

    private void SetHover(Border border, bool hovered)
    {
        if (border.BindingContext is not SidebarItemVisual visual)
            return;
        ApplyItemChrome(border, IsSelected(visual.Item.Key), hovered, focused: false);
    }

    private void SetFocusChrome(Border border, bool focused)
    {
        if (border.BindingContext is not SidebarItemVisual visual)
            return;
        ApplyItemChrome(border, IsSelected(visual.Item.Key), hovered: false, focused: focused);
    }

    private void ApplyItemChrome(Border border, bool selected, bool hovered, bool focused)
    {
        if (border.BindingContext is not SidebarItemVisual visual)
            return;

        var text = selected
            ? SidebarTokens.Color("SidebarActiveTextLight", "SidebarActiveTextDark")
            : SidebarTokens.Color("SidebarTextLight", "SidebarTextDark");
        var background = selected
            ? SidebarTokens.Color("SidebarActiveItemLight", "SidebarActiveItemDark")
            : hovered
                ? SidebarTokens.Color("SidebarItemHoverLight", "SidebarItemHoverDark")
                : Colors.Transparent;

        border.BackgroundColor = background;
        border.StrokeThickness = focused ? 1 : 0;
        border.Stroke = focused
            ? SidebarTokens.Color("SidebarIndicator", "SidebarIndicator")
            : Colors.Transparent;
        visual.Label.TextColor = text;
        visual.Icon.TextColor = text;
        visual.Chevron.TextColor = selected
            ? text
            : SidebarTokens.Color("SidebarMutedLight", "SidebarMutedDark");
        visual.Label.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
    }

    private string? FindParentKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        foreach (var item in _items)
        {
            if (item.Children.Any(child => child.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
                return item.Key;
        }

        return null;
    }

    private void EnableKeyboard(Border root, NavigationMenuNode item)
    {
#if WINDOWS
        if (root.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.Control native)
            return;

        native.IsTabStop = true;
        native.UseSystemFocusVisuals = false;
        native.GotFocus -= OnNativeGotFocus;
        native.LostFocus -= OnNativeLostFocus;
        native.KeyDown -= OnNativeKeyDown;
        native.GotFocus += OnNativeGotFocus;
        native.LostFocus += OnNativeLostFocus;
        native.KeyDown += OnNativeKeyDown;
        native.Tag = item;
#else
        _ = root;
        _ = item;
#endif
    }

#if WINDOWS
    private void OnNativeGotFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement native && FindBorder(native) is { } border)
            SetFocusChrome(border, true);
    }

    private void OnNativeLostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement native && FindBorder(native) is { } border)
            SetFocusChrome(border, false);
    }

    private void OnNativeKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key is not Windows.System.VirtualKey.Enter and not Windows.System.VirtualKey.Space)
            return;
        if (sender is Microsoft.UI.Xaml.FrameworkElement native
            && native.Tag is NavigationMenuNode item)
        {
            OnItemTapped(item);
            e.Handled = true;
        }
    }

    private Border? FindBorder(Microsoft.UI.Xaml.FrameworkElement native)
    {
        foreach (var visual in _visuals.Values)
        {
            if (ReferenceEquals(visual.Root.Handler?.PlatformView, native))
                return visual.Root;
        }

        return null;
    }
#endif

    private void OnSidebarSizeChanged(object? sender, EventArgs e)
        => ApplyNavViewport();

    private void OnNavScrollHandlerChanged(object? sender, EventArgs e)
        => EnableNativeScrolling();

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        ApplyNavViewport();
    }

    private void ApplyNavViewport()
    {
        if (Height <= 0)
            return;

        var header = BrandHeader.Height > 1 ? BrandHeader.Height : 64;
        var nav = Math.Max(0, Math.Floor(Height - header));
        if (nav < 32)
            return;

        if (Math.Abs(NavScroll.HeightRequest - nav) < 1)
            return;

        NavScroll.HeightRequest = nav;
        NavScroll.MaximumHeightRequest = nav;
        EnableNativeScrolling();
    }

    private void EnableNativeScrolling()
    {
#if WINDOWS
        var platform = NavScroll.Handler?.PlatformView as Microsoft.UI.Xaml.DependencyObject;
        var viewer = FindScrollViewer(platform);
        if (viewer == null)
            return;

        viewer.VerticalScrollMode = Microsoft.UI.Xaml.Controls.ScrollMode.Enabled;
        viewer.VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto;
        viewer.HorizontalScrollMode = Microsoft.UI.Xaml.Controls.ScrollMode.Disabled;
        viewer.HorizontalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Disabled;
        viewer.ZoomMode = Microsoft.UI.Xaml.Controls.ZoomMode.Disabled;
        viewer.BringIntoViewOnFocusChange = false;
#endif
    }

#if WINDOWS
    private static Microsoft.UI.Xaml.Controls.ScrollViewer? FindScrollViewer(Microsoft.UI.Xaml.DependencyObject? root)
    {
        if (root == null)
            return null;
        if (root is Microsoft.UI.Xaml.Controls.ScrollViewer viewer)
            return viewer;

        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindScrollViewer(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i));
            if (found != null)
                return found;
        }

        return null;
    }
#endif

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler != null)
        {
            EnableNativeScrolling();
            return;
        }

        SizeChanged -= OnSidebarSizeChanged;
        BrandHeader.SizeChanged -= OnSidebarSizeChanged;
        NavScroll.HandlerChanged -= OnNavScrollHandlerChanged;
        if (Microsoft.Maui.Controls.Application.Current != null)
            Microsoft.Maui.Controls.Application.Current.RequestedThemeChanged -= OnThemeChanged;
    }

    private sealed record SidebarItemVisual(
        NavigationMenuNode Item,
        Border Root,
        Label Icon,
        Label Label,
        Label Chevron,
        bool IsChild);
}
