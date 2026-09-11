namespace Microsoft.Maui.Storage;

/// <summary>
/// Minimal test-only shim for AuthenticationService remember-login preferences.
/// The Android app uses the real MAUI Preferences implementation; the plain
/// net10.0 offline-auth test project uses this in-memory version.
/// </summary>
public static class Preferences
{
    public static TestPreferences Default { get; } = new();
}

public sealed class TestPreferences
{
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

    public void Set(string key, bool value) => _values[key] = value;

    public bool Get(string key, bool defaultValue)
        => _values.TryGetValue(key, out var value) && value is bool result
            ? result
            : defaultValue;
}
