using AveroNova.App.UI.Controls.Common;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Resources;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Notifications;

public partial class NotificationsPage : ContentPage
{
    private readonly INotificationService _svc;

    private List<NotificationModel> _all = [];

    private bool _onlyUnread = false;

    public NotificationsPage(INotificationService svc)
    {
        InitializeComponent();

        _svc = svc;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await LoadAsync();
    }

    private async void OnRefreshing(
        object sender,
        EventArgs e)
    {
        await LoadAsync();

        Refresher.IsRefreshing = false;
    }

    private async void OnRefreshClicked(
        object sender,
        EventArgs e)
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _all = await _svc.GetAllAsync();

        RenderList();
    }

    private async void OnMarkAllReadClicked(
        object sender,
        EventArgs e)
    {
        await _svc.MarkAllReadAsync();

        await LoadAsync();
    }

    private void OnFilterUnreadClicked(
        object sender,
        EventArgs e)
    {
        _onlyUnread = !_onlyUnread;

        RenderList();
    }

    private void OnSearchChanged(
        object sender,
        TextChangedEventArgs e)
    {
        RenderList();
    }

    private void RenderList()
    {
        var query =
            SearchBar.Text?.Trim()
            ?? string.Empty;

        var shown = _all
            .Where(n =>
                !_onlyUnread ||
                !n.IsRead)

            .Where(n =>
                string.IsNullOrWhiteSpace(query)
                ||
                n.Title.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase)
                ||
                n.Message.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase))

            .OrderByDescending(n => n.CreatedAt)
            .ToList();

        LblCount.Text =
            $"{_all.Count(n => !n.IsRead)} unread";

        NotificationList.Children.Clear();

        if (shown.Count == 0)
        {
            NotificationList.Children.Add(
                new Label
                {
                    Text = "No notifications found.",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#64748B"),
                    HorizontalOptions =
                        LayoutOptions.Center,
                    Margin =
                        new Thickness(0, 40)
                });

            return;
        }

        foreach (var notification in shown)
        {
            NotificationList.Children.Add(
                BuildRow(notification));
        }
    }

    private View BuildRow(NotificationModel notification)
    {
        var (iconBackground, iconColor) =
     notification.Category switch
     {
         NotificationCategory.Invoice =>
             ("#EFF6FF", "#2563EB"),

         NotificationCategory.Payment =>
             ("#ECFDF5", "#059669"),

         NotificationCategory.Inventory =>
             ("#FFF7ED", "#EA580C"),

         NotificationCategory.System =>
             ("#F5F3FF", "#7C3AED"),

         NotificationCategory.Sync =>
             ("#ECFEFF", "#0891B2"),

         NotificationCategory.Subscription =>
             ("#FFFBEB", "#D97706"),

         _ =>
             ("#F8FAFC", "#64748B")
     };

        bool isDark =
            Microsoft.Maui.Controls.Application.Current?
                .RequestedTheme == AppTheme.Dark;

        var border = new Border
        {
            BackgroundColor = notification.IsRead
                ? isDark
                    ? Color.FromArgb("#1E293B")
                    : Color.FromArgb("#F8FAFC")
                : isDark
                    ? Color.FromArgb("#1E293B")
                    : Color.FromArgb("#FFFFFF"),

            Stroke = Color.FromArgb("#E2E8F0"),

            StrokeThickness = 1,

            StrokeShape =
                new RoundRectangle
                {
                    CornerRadius =
                        new CornerRadius(12)
                },

            Padding =
                new Thickness(14, 12)
        };

        var grid = new Grid
        {
            ColumnDefinitions =
                new ColumnDefinitionCollection(
                    new ColumnDefinition(
                        GridLength.Auto),

                    new ColumnDefinition(
                        GridLength.Star),

                    new ColumnDefinition(
                        GridLength.Auto)),

            ColumnSpacing = 12
        };

        // =========================================================
        // ICON
        // =========================================================

        var iconBorder = new Border
        {
            WidthRequest = 40,
            HeightRequest = 40,

            BackgroundColor =
                Color.FromArgb(iconBackground),

            StrokeThickness = 0,

            StrokeShape =
                new RoundRectangle
                {
                    CornerRadius =
                        new CornerRadius(20)
                },

            VerticalOptions =
                LayoutOptions.Start
        };

        var iconText =
            Microsoft.Maui.Controls.Application.Current?
                .Resources["IconNotifications"]
                ?.ToString()
                ?? "🔔";

        iconBorder.Content =
            new Label
            {
                Text = iconText,
                FontSize = 16,

                TextColor =
                    Color.FromArgb(iconColor),

                HorizontalOptions =
                    LayoutOptions.Center,

                VerticalOptions =
                    LayoutOptions.Center
            };

        // =========================================================
        // CONTENT
        // =========================================================

        var info =
            new VerticalStackLayout
            {
                Spacing = 3
            };

        info.Children.Add(
            new Label
            {
                Text = notification.Title,

                FontSize = 14,

                FontAttributes =
                    notification.IsRead
                        ? FontAttributes.None
                        : FontAttributes.Bold
            });

        info.Children.Add(
            new Label
            {
                Text = notification.Message,

                FontSize = 12,

                TextColor =
                    Color.FromArgb("#64748B"),

                LineBreakMode =
                    LineBreakMode.TailTruncation,

                MaxLines = 2
            });

        info.Children.Add(
            new Label
            {
                Text = notification.TimeAgo,

                FontSize = 11,

                TextColor =
                    Color.FromArgb("#94A3B8")
            });

        // =========================================================
        // UNREAD DOT
        // =========================================================

        if (!notification.IsRead)
        {
            var dot =
                new Border
                {
                    WidthRequest = 8,
                    HeightRequest = 8,

                    BackgroundColor =
                        Color.FromArgb("#2563EB"),

                    StrokeThickness = 0,

                    StrokeShape =
                        new RoundRectangle
                        {
                            CornerRadius =
                                new CornerRadius(4)
                        },

                    VerticalOptions =
                        LayoutOptions.Start,

                    Margin =
                        new Thickness(0, 6, 0, 0)
                };

            grid.Add(dot, 2, 0);
        }

        grid.Add(iconBorder, 0, 0);
        grid.Add(info, 1, 0);

        border.Content = grid;

        // =========================================================
        // MARK AS READ
        // =========================================================

        var tap =
            new TapGestureRecognizer();

        tap.Tapped += async (_, _) =>
        {
            if (!notification.IsRead)
            {
                await _svc.MarkAsReadAsync(
                    notification.Id);

                notification.IsRead = true;

                RenderList();
            }
        };

        border.GestureRecognizers.Add(tap);

        return border;
    }
}