using System.Collections.Concurrent;
using AveroNova.Application.Interfaces.Auth;
using Microsoft.Extensions.Options;

namespace AveroNova.Infrastructure.Auth;

public sealed class LoginAttemptProtector : ILoginAttemptProtector
{
    private readonly ConcurrentDictionary<string, AttemptState> _attempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly AuthSecurityOptions _options;

    public LoginAttemptProtector(IOptions<AuthSecurityOptions> options) => _options = options.Value;

    public bool IsBlocked(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        if (!_attempts.TryGetValue(Normalize(key), out var state)) return false;
        if (state.BlockedUntilUtc is null) return false;
        if (state.BlockedUntilUtc > DateTime.UtcNow) return true;
        _attempts.TryRemove(Normalize(key), out _);
        return false;
    }

    public void RecordFailure(string key)
    {
        var k = Normalize(key);
        var state = _attempts.AddOrUpdate(
            k,
            _ => new AttemptState { Failures = 1 },
            (_, existing) =>
            {
                existing.Failures++;
                if (existing.Failures >= Math.Max(3, _options.MaxFailedAttempts))
                    existing.BlockedUntilUtc = DateTime.UtcNow.AddMinutes(Math.Max(1, _options.LockoutMinutes));
                return existing;
            });
        _ = state;
    }

    public void Reset(string key) => _attempts.TryRemove(Normalize(key), out _);

    private static string Normalize(string key) => key.Trim().ToLowerInvariant();

    private sealed class AttemptState
    {
        public int Failures { get; set; }
        public DateTime? BlockedUntilUtc { get; set; }
    }
}
