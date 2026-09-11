using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Settings;

public partial class SettingsPage : ContentPage, IHostedPage
{
    private readonly ISettingsService _service;
    private AppSettings _settings = new();

    private Picker _theme = null!;
    private Picker _language = null!;
    private Picker _date = null!;
    private Picker _currency = null!;
    private Picker _timeZone = null!;
    private Entry _accent = null!;
    private Switch _compact = null!;
    private Switch _notifications = null!;
    private Switch _autoSync = null!;
    private Switch _offline = null!;
    private Switch _remember = null!;
    private Label _message = null!;

    private static readonly string[] Themes = ["System", "Light", "Dark"];
    private static readonly string[] Languages = ["English", "Hindi", "Urdu"];
    private static readonly string[] LanguageCodes = ["en", "hi", "ur"];
    private static readonly string[] Dates = ["dd MMM yyyy", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd"];
    private static readonly string[] Currencies = ["USD ($)", "INR (₹)", "EUR (€)", "GBP (£)", "AED (د.إ)"];
    private static readonly string[] CurrencyCodes = ["USD", "INR", "EUR", "GBP", "AED"];
    private static readonly string[] CurrencySymbols = ["$", "₹", "€", "£", "د.إ"];
    private static readonly string[] TimeZones = ["UTC", "Asia/Kolkata", "Asia/Dubai", "Europe/London", "America/New_York"];

    public SettingsPage(ISettingsService service)
    {
        InitializeComponent();
        _service = service;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }

    public async Task LoadForHostAsync()
    {
        try
        {
            _settings = await _service.GetAsync() ?? new AppSettings();
            BuildContent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] Load failed: {ex}");
            BuildFallback(ex.Message);
        }
    }

    private async void OnRefreshing(object s, EventArgs e)
    {
        try
        {
            await LoadForHostAsync();
        }
        finally
        {
            Refresher.IsRefreshing = false;
        }
    }

    private void BuildContent()
    {
        SettingsContent.Children.Clear();

        _theme = MakePicker(Themes, ClampIndex((int)_settings.Theme, Themes.Length));
        _accent = new Entry
        {
            Text = string.IsNullOrWhiteSpace(_settings.AccentColor) ? "#2563EB" : _settings.AccentColor,
            HorizontalTextAlignment = TextAlignment.Center,
            WidthRequest = 210
        };
        _compact = new Switch { IsToggled = _settings.CompactMode };

        SettingsContent.Children.Add(Card(
            "Appearance",
            Row("Theme", _theme),
            Row("Accent color", _accent),
            Row("Compact mode", _compact)));

        _language = MakePicker(Languages, FindIndex(LanguageCodes, _settings.Language));
        _date = MakePicker(Dates, FindIndex(Dates, _settings.DateFormat));
        _currency = MakePicker(Currencies, FindIndex(CurrencyCodes, _settings.Currency));
        _timeZone = MakePicker(TimeZones, FindIndex(TimeZones, _settings.TimeZone));

        SettingsContent.Children.Add(Card(
            "Regional",
            Row("Language", _language),
            Row("Date format", _date),
            Row("Currency", _currency),
            Row("Time zone", _timeZone)));

        _notifications = new Switch { IsToggled = _settings.Notifications };
        _autoSync = new Switch { IsToggled = _settings.AutoSync };
        _offline = new Switch { IsToggled = _settings.OfflineMode };
        _remember = new Switch { IsToggled = _settings.RememberLogin };

        SettingsContent.Children.Add(Card(
            "Sync & Preferences",
            Row("Notifications", _notifications),
            Row("Auto-sync", _autoSync),
            Row("Offline mode", _offline),
            Row("Remember login", _remember)));

        _message = new Label
        {
            IsVisible = false,
            HorizontalTextAlignment = TextAlignment.Center,
            FontSize = 12
        };
        SettingsContent.Children.Add(_message);

        var save = new Button
        {
            Text = "Save Settings",
            HeightRequest = 46,
            CornerRadius = 10,
            BackgroundColor = Color.FromArgb("#2563EB"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Fill,
            MaximumWidthRequest = 500
        };
        save.Clicked += OnSaveClicked;
        SettingsContent.Children.Add(save);
    }

    private void BuildFallback(string detail)
    {
        SettingsContent.Children.Clear();
        SettingsContent.Children.Add(new Border
        {
            Stroke = Color.FromArgb("#FECACA"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            BackgroundColor = Color.FromArgb("#FEF2F2"),
            Padding = new Thickness(16),
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label { Text = "Settings could not be loaded", FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#991B1B") },
                    new Label { Text = "The app is still running. Pull down to retry.", FontSize = 12, TextColor = Color.FromArgb("#7F1D1D") }
                }
            }
        });
        System.Diagnostics.Debug.WriteLine($"[Settings] Fallback detail: {detail}");
    }

    private async void OnSaveClicked(object? s, EventArgs e)
    {
        try
        {
            _settings.Theme = (ThemeMode)ClampIndex(_theme.SelectedIndex, Themes.Length);
            _settings.AccentColor = _accent.Text?.Trim() ?? "#2563EB";
            _settings.CompactMode = _compact.IsToggled;
            _settings.Language = LanguageCodes[ClampIndex(_language.SelectedIndex, LanguageCodes.Length)];
            _settings.DateFormat = Dates[ClampIndex(_date.SelectedIndex, Dates.Length)];

            var ci = ClampIndex(_currency.SelectedIndex, CurrencyCodes.Length);
            _settings.Currency = CurrencyCodes[ci];
            _settings.CurrencySymbol = CurrencySymbols[ci];
            _settings.TimeZone = TimeZones[ClampIndex(_timeZone.SelectedIndex, TimeZones.Length)];
            _settings.Notifications = _notifications.IsToggled;
            _settings.AutoSync = _autoSync.IsToggled;
            _settings.OfflineMode = _offline.IsToggled;
            _settings.RememberLogin = _remember.IsToggled;

            var result = await _service.SaveAsync(_settings);
            _message.Text = result.Ok ? "Settings saved locally and queued for sync." : result.Error ?? "Settings could not be saved.";
            _message.TextColor = result.Ok ? Color.FromArgb("#059669") : Color.FromArgb("#DC2626");
            _message.IsVisible = true;

            if (result.Ok && Microsoft.Maui.Controls.Application.Current is not null)
            {
                Microsoft.Maui.Controls.Application.Current.UserAppTheme = _settings.Theme switch
                {
                    ThemeMode.Dark => AppTheme.Dark,
                    ThemeMode.Light => AppTheme.Light,
                    _ => AppTheme.Unspecified
                };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] Save failed: {ex}");
            _message.Text = "Settings could not be saved. The app is still running.";
            _message.TextColor = Color.FromArgb("#DC2626");
            _message.IsVisible = true;
        }
    }

    private static int FindIndex(string[] values, string? value)
    {
        var i = Array.IndexOf(values, value ?? string.Empty);
        return i < 0 ? 0 : i;
    }

    private static int ClampIndex(int index, int length) =>
        index < 0 || index >= length ? 0 : index;

    private static Picker MakePicker(IEnumerable<string> values, int index) => new()
    {
        ItemsSource = values.ToList(),
        SelectedIndex = index,
        HorizontalTextAlignment = TextAlignment.Center,
        WidthRequest = 210
    };

    private static Grid Row(string label, View control)
    {
        var g = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 12
        };
        g.Add(new Label { Text = label, VerticalOptions = LayoutOptions.Center, TextColor = Color.FromArgb("#334155") }, 0, 0);
        g.Add(control, 1, 0);
        return g;
    }

    private static Border Card(string title, params View[] rows)
    {
        var stack = new VerticalStackLayout { Spacing = 12 };
        stack.Children.Add(new Label { Text = title, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#0F172A") });
        stack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#E2E8F0") });
        foreach (var row in rows) stack.Children.Add(row);

        return new Border
        {
            Stroke = Color.FromArgb("#E2E8F0"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
            BackgroundColor = Colors.White,
            Padding = new Thickness(16),
            MaximumWidthRequest = 760,
            Content = stack
        };
    }
}
