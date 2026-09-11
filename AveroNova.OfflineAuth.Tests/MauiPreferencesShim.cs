namespace Microsoft.Maui.Storage;

/// <summary>
/// Minimal test-only shim for AuthenticationService remember-login preferences.
/// The Android app uses the real MAUI Preferences implementation; the plain
/// net10.0 offline-auth test project uses this in-memory version.
///
/// Older orchestration tests predate the Remember Me UI and seed a valid local
/// session directly before calling TryAutoLoginAsync. For those fixtures the
/// remember-login preference defaults to true until a test explicitly sets it.
/// Production MAUI Preferences still default to false in AuthenticationService.
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
    {
        if (_values.TryGetValue(key, out var value) && value is bool result)
            return result;

        return string.Equals(key, "auth.remember_login", StringComparison.Ordinal)
            ? true
            : defaultValue;
    }
}
