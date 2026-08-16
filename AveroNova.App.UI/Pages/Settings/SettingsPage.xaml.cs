using AveroNova.App.UI.Controls.Common;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Resources;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Settings;

public partial class SettingsPage : ContentPage
{
    private readonly ISettingsService _svc;
    private AppSettings? _settings;

    public SettingsPage(ISettingsService svc)
    {
        InitializeComponent();
        _svc = svc;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _settings = _svc.Get();

        BuildContent();
    }

    private void OnRefreshing(object s, EventArgs e)
    {
        _settings = _svc.Get();

        BuildContent();

        Refresher.IsRefreshing = false;
    }

    private void BuildContent()
    {
        SettingsContent.Children.Clear();

        // =====================================================
        // APPEARANCE
        // =====================================================

        var appearanceCard = new Border
        {
            Style = (Style)Resources["AppCard"]
        };

        var aVsl = new VerticalStackLayout
        {
            Spacing = 12
        };

        aVsl.Children.Add(new Label
        {
            Text = "Appearance",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        });

        aVsl.Children.Add(new BoxView
        {
            Style = (Style)Resources["Divider"]
        });

        // Theme
        var themeRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            Margin = new Thickness(0, 4)
        };

        themeRow.Add(
            new Label
            {
                Text = "Dark mode",
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            },
            0,
            0);

        var themeSwitch = new Switch
        {
            IsToggled = _settings?.Theme == ThemeMode.Dark
        };

        themeSwitch.Toggled += (_, _) =>
        {
            if (_settings == null)
                return;

            var mode = themeSwitch.IsToggled
                ? ThemeMode.Dark
                : ThemeMode.Light;

            _svc.SetTheme(mode);

            _settings.Theme = mode;
        };

        themeRow.Add(themeSwitch, 1, 0);

        aVsl.Children.Add(themeRow);

        // Compact mode
        var compactRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            Margin = new Thickness(0, 4)
        };

        compactRow.Add(
            new Label
            {
                Text = "Compact mode",
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            },
            0,
            0);

        var compactSwitch = new Switch
        {
            IsToggled = _settings?.CompactMode ?? false
        };

        compactSwitch.Toggled += (_, _) =>
        {
            if (_settings == null)
                return;

            _settings.CompactMode = compactSwitch.IsToggled;

            _svc.Save(_settings);
        };

        compactRow.Add(compactSwitch, 1, 0);

        aVsl.Children.Add(compactRow);

        appearanceCard.Content = aVsl;


        // =====================================================
        // SYNC & DATA
        // =====================================================

        var syncCard = new Border
        {
            Style = (Style)Resources["AppCard"]
        };

        var sVsl = new VerticalStackLayout
        {
            Spacing = 12
        };

        sVsl.Children.Add(new Label
        {
            Text = "Sync & Data",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        });

        sVsl.Children.Add(new BoxView
        {
            Style = (Style)Resources["Divider"]
        });

        // Auto Sync
        var autoSyncRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            Margin = new Thickness(0, 4)
        };

        autoSyncRow.Add(
            new Label
            {
                Text = "Auto-sync",
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            },
            0,
            0);

        var autoSyncSwitch = new Switch
        {
            IsToggled = _settings?.AutoSync ?? true
        };

        autoSyncSwitch.Toggled += (_, _) =>
        {
            if (_settings == null)
                return;

            _settings.AutoSync = autoSyncSwitch.IsToggled;

            _svc.Save(_settings);
        };

        autoSyncRow.Add(autoSyncSwitch, 1, 0);

        sVsl.Children.Add(autoSyncRow);

        syncCard.Content = sVsl;


        // =====================================================
        // NOTIFICATIONS
        // =====================================================

        var notificationsCard = new Border
        {
            Style = (Style)Resources["AppCard"]
        };

        var nVsl = new VerticalStackLayout
        {
            Spacing = 12
        };

        nVsl.Children.Add(new Label
        {
            Text = "Notifications",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        });

        nVsl.Children.Add(new BoxView
        {
            Style = (Style)Resources["Divider"]
        });

        var notifRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            Margin = new Thickness(0, 4)
        };

        notifRow.Add(
            new Label
            {
                Text = "Enable notifications",
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            },
            0,
            0);

        var notifSwitch = new Switch
        {
            IsToggled = _settings?.Notifications ?? true
        };

        notifSwitch.Toggled += (_, _) =>
        {
            if (_settings == null)
                return;

            _settings.Notifications = notifSwitch.IsToggled;

            _svc.Save(_settings);
        };

        notifRow.Add(notifSwitch, 1, 0);

        nVsl.Children.Add(notifRow);

        notificationsCard.Content = nVsl;


        // =====================================================
        // ADD CARDS
        // =====================================================

        SettingsContent.Children.Add(appearanceCard);
        SettingsContent.Children.Add(syncCard);
        SettingsContent.Children.Add(notificationsCard);
    }
}