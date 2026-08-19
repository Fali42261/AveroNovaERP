using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Controls.Notifications;

/// <summary>
/// Non-blocking toast card hosted on the current page. Empty space is not an overlay:
/// only the toast itself occupies hit-test area, so the rest of the UI stays usable.
/// </summary>
public sealed class AppToastHost : Border
{
    private const double DesktopWidth = 400;
    private const uint AnimationMs = 180;
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(5);

    private readonly BoxView _accent;
    private readonly BoxView _statusDot;
    private readonly Label _titleLabel;
    private readonly Label _messageLabel;
    private readonly Label _closeLabel;
    private CancellationTokenSource? _dismissCts;
    private int _showGeneration;
    private Page? _page;

    public AppToastHost()
    {
        StrokeThickness = 1;
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) };
        Padding = 0;
        HorizontalOptions = LayoutOptions.End;
        VerticalOptions = LayoutOptions.Start;
        IsVisible = false;
        Opacity = 0;
        ZIndex = 10000;
        AutomationId = "AppToast";

        this.SetAppThemeColor(
            BackgroundColorProperty,
            Res("CardBackground", "#FFFFFF"),
            Res("CardBackgroundDark", "#1E293B"));
        this.SetAppTheme(
            StrokeProperty,
            new SolidColorBrush(Res("BorderColor", "#E2E8F0")),
            new SolidColorBrush(Res("BorderColorDark", "#334155")));

        _accent = new BoxView
        {
            WidthRequest = 4,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        _statusDot = new BoxView
        {
            WidthRequest = 10,
            HeightRequest = 10,
            CornerRadius = 5,
            VerticalOptions = LayoutOptions.Center
        };

        _titleLabel = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 2
        };
        _titleLabel.SetAppThemeColor(
            Label.TextColorProperty,
            Res("TextPrimary", "#0F172A"),
            Res("TextPrimaryDark", "#F1F5F9"));

        _messageLabel = new Label
        {
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 4
        };
        _messageLabel.SetAppThemeColor(
            Label.TextColorProperty,
            Res("TextSecondary", "#64748B"),
            Res("TextSecondaryDark", "#94A3B8"));

        _closeLabel = new Label
        {
            Text = "x",
            FontSize = 16,
            Padding = new Thickness(10, 4, 2, 4),
            VerticalOptions = LayoutOptions.Start,
            HorizontalTextAlignment = TextAlignment.Center
        };
        _closeLabel.SetAppThemeColor(
            Label.TextColorProperty,
            Res("TextTertiary", "#94A3B8"),
            Res("TextSecondaryDark", "#94A3B8"));
        _closeLabel.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(Dismiss)
        });
        SemanticProperties.SetDescription(_closeLabel, "Dismiss notification");
        SemanticProperties.SetHint(_closeLabel, "Closes this notification");

        var textStack = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children = { _titleLabel, _messageLabel }
        };

        var body = new Grid
        {
            Padding = new Thickness(12, 12, 12, 12),
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        body.Add(_statusDot, 0);
        body.Add(textStack, 1);
        body.Add(_closeLabel, 2);

        var layout = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            }
        };
        layout.Add(_accent, 0);
        layout.Add(body, 1);

        Content = layout;
        Shadow = new Shadow
        {
            Brush = Res("Gray900", "#111827"),
            Opacity = 0.16f,
            Radius = 16,
            Offset = new Point(0, 6)
        };
    }

    public static AppToastHost Install(ContentPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (page.Content is Grid wrapper
            && wrapper.Children.OfType<AppToastHost>().FirstOrDefault() is { } existing)
        {
            existing.AttachTo(page);
            return existing;
        }

        var host = new AppToastHost();
        var original = page.Content;
        var root = new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        if (original is not null)
        {
            original.HorizontalOptions = LayoutOptions.Fill;
            original.VerticalOptions = LayoutOptions.Fill;
            root.Add(original);
        }

        root.Add(host);
        page.Content = root;
        host.AttachTo(page);
        return host;
    }

    public async void Present(string title, string message, ToastKind kind, TimeSpan? duration = null)
    {
        var generation = ++_showGeneration;
        _dismissCts?.Cancel();
        _dismissCts = new CancellationTokenSource();
        var token = _dismissCts.Token;

        ApplyKind(kind);
        _titleLabel.Text = title;
        _messageLabel.Text = message;
        _messageLabel.IsVisible = !string.IsNullOrWhiteSpace(message);
        ApplyLayout();

        IsVisible = true;
        Opacity = 0;
        TranslationX = 20;

        try
        {
            await Task.WhenAll(
                this.FadeToAsync(1, AnimationMs, Easing.CubicOut),
                this.TranslateToAsync(0, 0, AnimationMs, Easing.CubicOut));

            if (generation != _showGeneration)
                return;

            await Task.Delay(duration ?? DefaultDuration, token);
            if (generation == _showGeneration)
                await DismissAsync();
        }
        catch (TaskCanceledException)
        {
            // Replaced by a newer toast or dismissed by the user.
        }
    }

    public void Dismiss()
        => _ = DismissAsync();

    public async Task DismissAsync()
    {
        _dismissCts?.Cancel();
        if (!IsVisible)
            return;

        var generation = _showGeneration;
        await Task.WhenAll(
            this.FadeToAsync(0, 140, Easing.CubicIn),
            this.TranslateToAsync(16, 0, 140, Easing.CubicIn));

        if (generation != _showGeneration)
            return;

        IsVisible = false;
        TranslationX = 0;
    }

    private void AttachTo(Page page)
    {
        if (ReferenceEquals(_page, page))
            return;

        if (_page is not null)
            _page.SizeChanged -= OnPageSizeChanged;

        _page = page;
        _page.SizeChanged += OnPageSizeChanged;
        Unloaded -= OnUnloaded;
        Unloaded += OnUnloaded;
        ApplyLayout();
    }

    private void OnPageSizeChanged(object? sender, EventArgs e) => ApplyLayout();

    private void OnUnloaded(object? sender, EventArgs e)
    {
        _dismissCts?.Cancel();
        if (_page is not null)
            _page.SizeChanged -= OnPageSizeChanged;
        Unloaded -= OnUnloaded;
        _page = null;
    }

    private void ApplyLayout()
    {
        var width = _page?.Width ?? Width;
        if (width <= 0)
        {
            var display = DeviceDisplay.MainDisplayInfo;
            width = display.Width / display.Density;
        }

        var compact = width <= ResponsiveBreakpoints.CompactMaxWidth;
        var side = compact ? 16 : 24;
        var available = Math.Max(240, width - (side * 2));
        var belowHeader = _page is MainPage;
        var top = belowHeader
            ? (compact ? 68 : 80)
            : (compact ? 16 : 24);

        Margin = new Thickness(side, top, side, 16);

        if (available < 360)
        {
            HorizontalOptions = LayoutOptions.Fill;
            WidthRequest = -1;
            MaximumWidthRequest = available;
        }
        else
        {
            HorizontalOptions = LayoutOptions.End;
            var toastWidth = Math.Min(DesktopWidth, available);
            WidthRequest = toastWidth;
            MaximumWidthRequest = toastWidth;
        }

        VerticalOptions = LayoutOptions.Start;
    }

    private void ApplyKind(ToastKind kind)
    {
        var (accent, dot) = kind switch
        {
            ToastKind.Success => (Res("SuccessColor", "#10B981"), Res("SuccessColor", "#10B981")),
            ToastKind.Warning => (Res("WarningColor", "#F59E0B"), Res("WarningColor", "#F59E0B")),
            ToastKind.Error => (Res("ErrorColor", "#EF4444"), Res("ErrorColor", "#EF4444")),
            _ => (Res("PrimaryColor", "#2563EB"), Res("PrimaryColor", "#2563EB"))
        };

        _accent.Color = accent;
        _statusDot.Color = dot;
        ApplyKindStroke(kind);
    }

    private void ApplyKindStroke(ToastKind kind)
    {
        var light = kind switch
        {
            ToastKind.Success => Res("SuccessBorder", "#A7F3D0"),
            ToastKind.Warning => Res("WarningBorder", "#FDE68A"),
            ToastKind.Error => Res("ErrorBorder", "#FECACA"),
            _ => Res("InfoBorder", "#BFDBFE")
        };

        this.SetAppTheme(
            StrokeProperty,
            new SolidColorBrush(light),
            new SolidColorBrush(Res("BorderColorDark", "#334155")));
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
