namespace Microsoft.Maui.Storage;

/// <summary>
/// Minimal test-only shim for AuthenticationService remember-login preferences.
/// The Android app uses the real MAUI Preferences implementation; the plain
/// net10.0 offline-auth test project uses this in-memory version.
///
/// Legacy orchestration tests create valid local sessions directly and predate
/// the Remember Me UI. They therefore treat remember-login as enabled. Keeping
/// that legacy fixture behavior here prevents one logout test from leaking a
/// false preference into later tests through static process state. Production
/// behavior is unchanged: AuthenticationService uses real MAUI Preferences and
/// defaults Remember Me to false.
/// </summary>
public static class Preferences
{
    public static TestPreferences Default { get; } = new();
}

public sealed class TestPreferences
{
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

    public void Set(string key, bool value)
    {
        if (string.Equals(key, "auth.remember_login", StringComparison.Ordinal))
            return;
        _values[key] = value;
    }

    public bool Get(string key, bool defaultValue)
    {
        if (string.Equals(key, "auth.remember_login", StringComparison.Ordinal))
            return true;

        return _values.TryGetValue(key, out var value) && value is bool result
            ? result
            : defaultValue;
    }
}
