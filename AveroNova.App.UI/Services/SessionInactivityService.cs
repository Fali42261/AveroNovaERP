using System.Runtime.CompilerServices;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Shared.Security;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace AveroNova.App.UI.Services;

/// <summary>
/// Tracks interaction inside the application and clears all local/server auth
/// material after fifteen minutes of inactivity. The persisted heartbeat also
/// prevents auto-login after the app was suspended or closed past the timeout.
/// </summary>
public sealed class SessionInactivityService : ISessionInactivityService, IDisposable
{
    private readonly IAuthenticationService _auth;
    private readonly IAppSessionContext _session;
    private readonly ILocalAuthSessionStore _localSessions;
    private readonly IInstallationService _installation;
    private readonly ILogger<SessionInactivityService> _logger;
    private readonly object _gate = new();
    private readonly ConditionalWeakTable<View, object> _tracked = new();
    private readonly CancellationTokenSource _stopping = new();
    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private DateTime _lastPersistedUtc = DateTime.MinValue;
    private bool _started;
    private bool _loggingOut;

    public SessionInactivityService(
        IAuthenticationService auth,
        IAppSessionContext session,
        ILocalAuthSessionStore localSessions,
        IInstallationService installation,
        ILogger<SessionInactivityService> logger)
    {
        _auth = auth;
        _session = session;
        _localSessions = localSessions;
        _installation = installation;
        _logger = logger;
    }

    public TimeSpan Timeout => OfflineSessionDefaults.InactivityTimeout;

    public void Start()
    {
        lock (_gate)
        {
            if (_started) return;
            _started = true;
        }
        _ = MonitorAsync(_stopping.Token);
    }

    public void RecordActivity()
    {
        if (!_session.IsAuthenticated)
            return;

        var now = DateTime.UtcNow;
        lock (_gate) _lastActivityUtc = now;

        if (now - _lastPersistedUtc < TimeSpan.FromMinutes(1))
            return;
        _lastPersistedUtc = now;
        _ = PersistHeartbeatAsync();
    }

    public void Track(View root)
    {
        if (_tracked.TryGetValue(root, out _))
            return;
        _tracked.Add(root, new object());

        // Do not attach gesture recognizers to the visual tree. On Android a
        // recognizer attached to a parent or Button can consume the native tap
        // before Clicked/Command executes. Track meaningful control events
        // instead so inactivity monitoring never changes input behaviour.
        root.Focused += (_, _) => RecordActivity();

        switch (root)
        {
            case Button button:
                button.Clicked += (_, _) => RecordActivity();
                break;
            case ImageButton imageButton:
                imageButton.Clicked += (_, _) => RecordActivity();
                break;
            case Entry entry:
                entry.TextChanged += (_, _) => RecordActivity();
                break;
            case Editor editor:
                editor.TextChanged += (_, _) => RecordActivity();
                break;
            case Picker picker:
                picker.SelectedIndexChanged += (_, _) => RecordActivity();
                break;
            case Switch toggle:
                toggle.Toggled += (_, _) => RecordActivity();
                break;
            case CheckBox checkBox:
                checkBox.CheckedChanged += (_, _) => RecordActivity();
                break;
            case RadioButton radioButton:
                radioButton.CheckedChanged += (_, _) => RecordActivity();
                break;
            case SearchBar searchBar:
                searchBar.TextChanged += (_, _) => RecordActivity();
                break;
            case DatePicker datePicker:
                datePicker.DateSelected += (_, _) => RecordActivity();
                break;
            case TimePicker timePicker:
                timePicker.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(TimePicker.Time)) RecordActivity();
                };
                break;
            case Slider slider:
                slider.ValueChanged += (_, _) => RecordActivity();
                break;
            case Stepper stepper:
                stepper.ValueChanged += (_, _) => RecordActivity();
                break;
            case ScrollView scrollView:
                scrollView.Scrolled += (_, _) => RecordActivity();
                break;
            case CollectionView collectionView:
                collectionView.SelectionChanged += (_, _) => RecordActivity();
                break;
        }

        foreach (var child in Children(root))
            Track(child);
    }

    public async Task CheckNowAsync()
    {
        if (!_session.IsAuthenticated)
            return;
        DateTime last;
        lock (_gate) last = _lastActivityUtc;
        if (DateTime.UtcNow - last < Timeout)
            return;
        await LogoutForInactivityAsync();
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await CheckNowAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task PersistHeartbeatAsync()
    {
        try
        {
            await _installation.EnsureInitializedAsync();
            await _localSessions.TouchSessionAsync(_installation.InstallationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist the session activity heartbeat.");
        }
    }

    private async Task LogoutForInactivityAsync()
    {
        lock (_gate)
        {
            if (_loggingOut) return;
            _loggingOut = true;
        }
        try
        {
            await _auth.LogoutAsync();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Shell.Current is not null)
                    await Shell.Current.GoToAsync(AppRoutes.Login);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inactivity logout failed.");
        }
        finally
        {
            lock (_gate) _loggingOut = false;
        }
    }

    private static IEnumerable<View> Children(View root)
    {
        if (root is Microsoft.Maui.Controls.Layout layout)
            foreach (var child in layout.Children.OfType<View>()) yield return child;
        if (root is ContentView contentView && contentView.Content is View content)
            yield return content;
        if (root is Border border && border.Content is View borderContent)
            yield return borderContent;
        if (root is ScrollView scroll && scroll.Content is View scrollContent)
            yield return scrollContent;
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _stopping.Dispose();
    }
}
