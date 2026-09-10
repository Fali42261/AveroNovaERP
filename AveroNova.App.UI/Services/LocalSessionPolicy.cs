using AveroNova.App.UI.Data;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public sealed class LocalSessionPolicy : ILocalSessionPolicy
{
    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly IInstallationService _installation;

    public LocalSessionPolicy(
        IDbContextFactory<LocalAppDbContext> dbFactory,
        IInstallationService installation)
    {
        _dbFactory = dbFactory;
        _installation = installation;
    }

    public async Task<bool> HasValidOfflineSessionAsync(CancellationToken cancellationToken = default)
    {
        await _installation.EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var installationId = _installation.InstallationId;

        var sessions = await db.Sessions.AsNoTracking()
            .Where(s => s.IsActive
                        && s.InstallationId == installationId
                        && s.OfflineExpiresAtUtc > now)
            .ToListAsync(cancellationToken);
        return sessions.Any(s =>
            (s.LastValidatedAtUtc ?? s.LastAuthenticatedAtUtc)
            > now - AveroNova.Shared.Security.OfflineSessionDefaults.InactivityTimeout);
    }
}
