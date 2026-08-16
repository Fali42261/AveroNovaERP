using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;


namespace AveroNova.App.UI.Pages.SyncCenter;

public partial class SyncCenterPage : ContentPage
{
    private readonly ISyncService _svc;
    private readonly IConnectivityService _conn;

    public SyncCenterPage(
        ISyncService svc,
        IConnectivityService conn)
    {
        InitializeComponent();

        _svc = svc;
        _conn = conn;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _conn.StatusChanged += OnStatusChanged;
        await BuildContentAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _conn.StatusChanged -= OnStatusChanged;
    }

    private async void OnStatusChanged(
        object? sender,
        ConnectivityStatus status)
    {
        await BuildContentAsync();
    }


    private async void OnRefreshing(object s, EventArgs e)
    {
        await BuildContentAsync();
        Refresher.IsRefreshing = false;
    }

    private async void OnSyncClicked(object s, EventArgs e)
    {
        if (!_conn.IsOnline)
        {
            await DisplayAlert("Offline", "Connect to the internet to sync.", "OK");
            return;
        }
        try
        {
            LblStatus.Text = "Syncing...";
            await _svc.SyncNowAsync();
            await DisplayAlert("Success", "Sync completed.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Sync Error", ex.Message, "OK");
        }
        await BuildContentAsync();
    }

    private async Task BuildContentAsync()
    {
        var connected = _conn.IsOnline;

        var pendingCount = _svc.PendingCount;
        var failedCount = _svc.FailedCount;
        var lastSync = _svc.LastSyncAt;
        LblStatus.Text = lastSync.HasValue
            ? $"Online • Last sync: {lastSync:dd MMM HH:mm}"
            : "Online • Never synced";

        SyncContent.Children.Clear();

        var connectivityCard = new Border { Style = (Style)Resources["AppCard"] };
        var cVsl = new VerticalStackLayout { Spacing = 12 };
        cVsl.Children.Add(new Label { Text = "Connectivity", FontSize = 14, FontAttributes = FontAttributes.Bold });
        cVsl.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });
        var connRow = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
        connRow.Add(new Label { Text = "Internet connection", FontSize = 14, VerticalOptions = LayoutOptions.Center }, 0, 0);
        var connBadge = new Border
        {
            BackgroundColor = connected ? Color.FromArgb("#ECFDF5") : Color.FromArgb("#FEF2F2"),
            StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            Padding = new Thickness(10, 4), VerticalOptions = LayoutOptions.Center
        };
        connBadge.Content = new Label { Text = connected ? "Online" : "Offline", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = connected ? Color.FromArgb("#059669") : Color.FromArgb("#DC2626") };
        connRow.Add(connBadge, 1, 0);
        cVsl.Children.Add(connRow);
        connectivityCard.Content = cVsl;

        var statsCard = new Border { Style = (Style)Resources["AppCard"] };
        var sVsl = new VerticalStackLayout { Spacing = 12 };
        sVsl.Children.Add(new Label { Text = "Sync Stats", FontSize = 14, FontAttributes = FontAttributes.Bold });
        sVsl.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });
        void AddStat(string label, string value)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), Margin = new Thickness(0, 2) };
            g.Add(new Label { Text = label, FontSize = 13, TextColor = Color.FromArgb("#64748B") }, 0, 0);
            g.Add(new Label { Text = value, FontSize = 13, FontAttributes = FontAttributes.Bold }, 1, 0);
            sVsl.Children.Add(g);
        }
        AddStat(
     "Pending operations",
     $"{_svc.PendingCount}");

        AddStat(
            "Failed operations",
            $"{_svc.FailedCount}");

        AddStat(
            "Last successful sync",
            _svc.LastSyncAt.HasValue
                ? _svc.LastSyncAt.Value.ToString("dd MMM yyyy HH:mm")
                : "Never");

        AddStat(
            "Sync status",
            _svc.IsSyncing
                ? "Syncing..."
                : "Idle");

        statsCard.Content = sVsl;

        var queueCard = new Border { Style = (Style)Resources["AppCard"] };
        var qVsl = new VerticalStackLayout { Spacing = 12 };
        qVsl.Children.Add(new Label { Text = "Recent Activity", FontSize = 14, FontAttributes = FontAttributes.Bold });
        qVsl.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });
        qVsl.Children.Add(new Label { Text = "No recent sync activity.", FontSize = 13, TextColor = Color.FromArgb("#64748B") });
        queueCard.Content = qVsl;

        SyncContent.Children.Add(connectivityCard);
        SyncContent.Children.Add(statsCard);
        SyncContent.Children.Add(queueCard);
    }
}
