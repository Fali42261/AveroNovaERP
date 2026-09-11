using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Settings;

public partial class SettingsPage : ContentPage, IHostedPage
{
    private const string RememberLoginPreferenceKey = "auth.remember_login";

    private readonly ISettingsService _service;
    private readonly IConnectivityService _connectivity;
    private AppSettings _settings = new();
    private bool _saving;
    private bool _building;

    private Picker _theme = null!;
    private Picker _language = null!;
    private Picker _date = null!;
    private Picker _currency = null!;
    private Picker _timeZone = null!;
    private Entry _accent = null!;
    private Switch _compact = null!;
    private Switch _notifications = null!;
    private Switch _offline = null!;
    private Switch _remember = null!;
    private Label _message = null!;
    private Button _saveButton = null!;

    // Must match ThemeMode enum order: Light=0, Dark=1, System=2.
    private static readonly string[] Themes = ["Light", "Dark", "System"];
    private static readonly string[] Languages = ["English", "Hindi", "Urdu"];
    private static readonly string[] LanguageCodes = ["en", "hi", "ur"];
    private static readonly string[] Dates = ["dd MMM yyyy", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd"];
    private static readonly string[] Currencies = ["USD ($)", "INR (₹)", "EUR (€)", "GBP (£)", "AED (د.إ)"];
    private static readonly string[] CurrencyCodes = ["USD", "INR", "EUR", "GBP", "AED"];
    private static readonly string[] CurrencySymbols = ["$", "₹", "€", "£", "د.إ"];
    private static readonly string[] TimeZones = ["UTC", "Asia/Kolkata", "Asia/Dubai", "Europe/London", "America/New_York"];

    public SettingsPage(ISettingsService service, IConnectivityService connectivity)
    {
        InitializeComponent();
        _service = service;
        _connectivity = connectivity;
        _connectivity.StatusChanged += OnConnectivityChanged;
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
            _settings.RememberLogin = Microsoft.Maui.Storage.Preferences.Default.Get(
                RememberLoginPreferenceKey,
                _settings.RememberLogin);

            // Sync is product-managed: online => sync, offline => pending queue.
            _settings.AutoSync = true;

            AppRegionalPreferences.Apply(
                _settings.Language,
                _settings.DateFormat,
                _settings.Currency,
                _settings.CurrencySymbol,
                _settings.TimeZone,
                persist: false);

            ApplyTheme(_settings.Theme);
            BuildContent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] Load failed: {ex}");
            BuildFallback();
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        try { await LoadForHostAsync(); }
        finally { Refresher.IsRefreshing = false; }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityStatus status)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_offline is not null)
                _offline.IsToggled = status == ConnectivityStatus.Offline;
        });
    }

    private void BuildContent()
    {
        _building = true;
        try
        {
            SettingsContent.Children.Clear();

            var dark = IsDark(_settings.Theme);
            var primaryText = dark ? Color.FromArgb("#F8FAFC") : Color.FromArgb("#0F172A");
            var secondaryText = dark ? Color.FromArgb("#CBD5E1") : Color.FromArgb("#334155");
            var surface = dark ? Color.FromArgb("#111827") : Colors.White;
            var border = dark ? Color.FromArgb("#334155") : Color.FromArgb("#E2E8F0");

            _theme = MakePicker(Themes, ClampIndex((int)_settings.Theme, Themes.Length), primaryText);
            _theme.SelectedIndexChanged += OnThemeChanged;
            _accent = new Entry
            {
                Text = string.IsNullOrWhiteSpace(_settings.AccentColor) ? "#2563EB" : _settings.AccentColor,
                HorizontalTextAlignment = TextAlignment.Center,
                WidthRequest = 210,
                TextColor = primaryText
            };
            _compact = new Switch { IsToggled = _settings.CompactMode };

            SettingsContent.Children.Add(Card(
                "Appearance", surface, border, primaryText, secondaryText,
                Row("Theme", _theme, secondaryText),
                Row("Accent color", _accent, secondaryText),
                Row("Compact mode", _compact, secondaryText)));

            _language = MakePicker(Languages, FindIndex(LanguageCodes, _settings.Language), primaryText);
            _date = MakePicker(Dates, FindIndex(Dates, _settings.DateFormat), primaryText);
            _currency = MakePicker(Currencies, FindIndex(CurrencyCodes, _settings.Currency), primaryText);
            _timeZone = MakePicker(TimeZones, FindIndex(TimeZones, _settings.TimeZone), primaryText);
            _language.SelectedIndexChanged += OnRegionalChanged;
            _date.SelectedIndexChanged += OnRegionalChanged;
            _currency.SelectedIndexChanged += OnRegionalChanged;
            _timeZone.SelectedIndexChanged += OnRegionalChanged;

            SettingsContent.Children.Add(Card(
                "Regional", surface, border, primaryText, secondaryText,
                Row("Language", _language, secondaryText),
                Row("Date format", _date, secondaryText),
                Row("Currency", _currency, secondaryText),
                Row("Time zone", _timeZone, secondaryText)));

            _notifications = new Switch { IsToggled = _settings.Notifications };
            _offline = new Switch { IsToggled = !_connectivity.IsOnline, IsEnabled = false };
            _remember = new Switch
            {
                IsToggled = Microsoft.Maui.Storage.Preferences.Default.Get(
                    RememberLoginPreferenceKey,
                    _settings.RememberLogin)
            };

            SettingsContent.Children.Add(Card(
                "Sync & Preferences", surface, border, primaryText, secondaryText,
                Row("Notifications", _notifications, secondaryText),
                Row("Offline mode (current status)", _offline, secondaryText),
                Row("Remember login", _remember, secondaryText)));

            SettingsContent.Children.Add(new Label
            {
                Text = "Synchronization runs automatically when internet is available. Offline changes remain queued until connection returns.",
                FontSize = 11,
                TextColor = secondaryText,
                HorizontalTextAlignment = TextAlignment.Center
            });

            _message = new Label
            {
                IsVisible = false,
                HorizontalTextAlignment = TextAlignment.Center,
                FontSize = 12
            };
            SettingsContent.Children.Add(_message);

            _saveButton = new Button
            {
                Text = "Save Settings",
                HeightRequest = 46,
                CornerRadius = 10,
                BackgroundColor = SafeAccentColor(_settings.AccentColor),
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Fill,
                MaximumWidthRequest = 500
            };
            _saveButton.Clicked += OnSaveClicked;
            SettingsContent.Children.Add(_saveButton);
        }
        finally
        {
            _building = false;
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_building) return;
        var mode = (ThemeMode)ClampIndex(_theme.SelectedIndex, Themes.Length);
        ApplyTheme(mode);
        ShowMessage("Theme selected. Tap Save Settings to keep it.", true);
    }

    private void OnRegionalChanged(object? sender, EventArgs e)
    {
        if (_building) return;
        var ci = ClampIndex(_currency.SelectedIndex, CurrencyCodes.Length);
        AppRegionalPreferences.Apply(
            LanguageCodes[ClampIndex(_language.SelectedIndex, LanguageCodes.Length)],
            Dates[ClampIndex(_date.SelectedIndex, Dates.Length)],
            CurrencyCodes[ci],
            CurrencySymbols[ci],
            TimeZones[ClampIndex(_timeZone.SelectedIndex, TimeZones.Length)],
            persist: false);
        ShowMessage("Regional preference selected. Tap Save Settings to keep it.", true);
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_saving) return;
        _saving = true;
        _saveButton.IsEnabled = false;
        _saveButton.Text = "Saving...";
        HideMessage();

        try
        {
            _settings.Theme = (ThemeMode)ClampIndex(_theme.SelectedIndex, Themes.Length);
            _settings.AccentColor = NormalizeAccent(_accent.Text);
            _settings.CompactMode = _compact.IsToggled;
            _settings.Language = LanguageCodes[ClampIndex(_language.SelectedIndex, LanguageCodes.Length)];
            _settings.DateFormat = Dates[ClampIndex(_date.SelectedIndex, Dates.Length)];
            var ci = ClampIndex(_currency.SelectedIndex, CurrencyCodes.Length);
            _settings.Currency = CurrencyCodes[ci];
            _settings.CurrencySymbol = CurrencySymbols[ci];
            _settings.TimeZone = TimeZones[ClampIndex(_timeZone.SelectedIndex, TimeZones.Length)];
            _settings.Notifications = _notifications.IsToggled;
            _settings.AutoSync = true;
            _settings.OfflineMode = !_connectivity.IsOnline;
            _settings.RememberLogin = _remember.IsToggled;

            var result = await _service.SaveAsync(_settings);
            if (!result.Ok)
            {
                ShowMessage(result.Error ?? "Settings could not be saved.", false);
                return;
            }

            Microsoft.Maui.Storage.Preferences.Default.Set(RememberLoginPreferenceKey, _settings.RememberLogin);
            AppRegionalPreferences.Apply(
                _settings.Language,
                _settings.DateFormat,
                _settings.Currency,
                _settings.CurrencySymbol,
                _settings.TimeZone,
                persist: true);
            ApplyTheme(_settings.Theme);

            BuildContent();
            ShowMessage("Settings saved successfully.", true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] Save failed: {ex}");
            ShowMessage("Settings could not be saved. Please try again.", false);
        }
        finally
        {
            _saving = false;
            if (_saveButton is not null)
            {
                _saveButton.IsEnabled = true;
                _saveButton.Text = "Save Settings";
            }
        }
    }

    private static void ApplyTheme(ThemeMode theme)
    {
        if (Microsoft.Maui.Controls.Application.Current is null) return;
        Microsoft.Maui.Controls.Application.Current.UserAppTheme = theme switch
        {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }

    private static bool IsDark(ThemeMode theme)
    {
        if (theme == ThemeMode.Dark) return true;
        if (theme == ThemeMode.Light) return false;
        return Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark;
    }

    private void BuildFallback()
    {
        SettingsContent.Children.Clear();
        SettingsContent.Children.Add(new Label
        {
            Text = "Settings could not be loaded. Pull down to retry.",
            TextColor = Color.FromArgb("#DC2626"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 30)
        });
    }

    private void HideMessage()
    {
        if (_message is not null) _message.IsVisible = false;
    }

    private void ShowMessage(string text, bool success)
    {
        if (_message is null) return;
        _message.Text = text;
        _message.TextColor = success ? Color.FromArgb("#059669") : Color.FromArgb("#DC2626");
        _message.IsVisible = true;
    }

    private static string NormalizeAccent(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 6 && !text.StartsWith('#')) text = "#" + text;
        return string.IsNullOrWhiteSpace(text) ? "#2563EB" : text;
    }

    private static Color SafeAccentColor(string? value)
    {
        try { return Color.FromArgb(NormalizeAccent(value)); }
        catch { return Color.FromArgb("#2563EB"); }
    }

    private static int FindIndex(string[] values, string? value)
    {
        var i = Array.IndexOf(values, value ?? string.Empty);
        return i < 0 ? 0 : i;
    }

    private static int ClampIndex(int index, int length) => index < 0 || index >= length ? 0 : index;

    private static Picker MakePicker(IEnumerable<string> values, int index, Color textColor) => new()
    {
        ItemsSource = values.ToList(),
        SelectedIndex = index,
        HorizontalTextAlignment = TextAlignment.Center,
        WidthRequest = 210,
        TextColor = textColor
    };

    private static Grid Row(string label, View control, Color textColor)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 12
        };
        grid.Add(new Label { Text = label, VerticalOptions = LayoutOptions.Center, TextColor = textColor }, 0, 0);
        grid.Add(control, 1, 0);
        return grid;
    }

    private static Border Card(string title, Color background, Color border, Color titleColor, Color dividerColor, params View[] rows)
    {
        var stack = new VerticalStackLayout { Spacing = 12 };
        stack.Children.Add(new Label { Text = title, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = titleColor });
        stack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = dividerColor });
        foreach (var row in rows) stack.Children.Add(row);
        return new Border
        {
            Stroke = border,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
            BackgroundColor = background,
            Padding = new Thickness(16),
            MaximumWidthRequest = 760,
            Content = stack
        };
    }
}
