using AveroNova.App.UI.Data;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AveroNova.App.UI.Services;

/// <summary>
/// Local installation identity and first-setup registration state (SQLite-backed).
/// InstallationId ≠ DeviceId ≠ UserId ≠ CompanyId.
/// </summary>
public sealed class InstallationService : IInstallationService
{
    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly ILogger<InstallationService> _logger;
    private readonly IStableDeviceIdProvider? _deviceIds;
    private LocalInstallationEntity? _cached;

    public InstallationService(
        IDbContextFactory<LocalAppDbContext> dbFactory,
        ILogger<InstallationService> logger,
        IStableDeviceIdProvider? deviceIds = null)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _deviceIds = deviceIds;
    }

    public Guid InstallationId => _cached?.InstallationId
        ?? throw new InvalidOperationException("Installation not initialized. Call EnsureInitializedAsync first.");

    public string DeviceId => _cached?.DeviceId
        ?? throw new InvalidOperationException("Installation not initialized. Call EnsureInitializedAsync first.");

    public bool IsRegistered => Status == LocalInstallationStatus.Registered;

    public LocalInstallationStatus Status => _cached?.Status ?? LocalInstallationStatus.NotRegistered;

    public bool CanCreateAccount => true;

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Installations.OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            var now = DateTime.UtcNow;
            row = new LocalInstallationEntity
            {
                Id = Guid.NewGuid(),
                InstallationId = Guid.NewGuid(),
                DeviceId = _deviceIds?.GetStableDeviceId() ?? Guid.NewGuid().ToString("N"),
                Status = LocalInstallationStatus.NotRegistered,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.Installations.Add(row);
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Created local installation InstallationId={InstallationId} DeviceId={DeviceId}",
                row.InstallationId, row.DeviceId);
        }

        _cached = row;
    }

    public async Task<LocalInstallationEntity> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is null)
            await EnsureInitializedAsync(cancellationToken);
        return _cached!;
    }

    public async Task MarkRegisteredAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Installations.OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken)
                  ?? throw new InvalidOperationException("Installation row missing.");

        if (row.Status == LocalInstallationStatus.Registered)
        {
            _cached = row;
            return;
        }

        var now = DateTime.UtcNow;
        row.Status = LocalInstallationStatus.Registered;
        row.RegisteredAtUtc = now;
        row.UserId = userId;
        row.CompanyId = companyId;
        row.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        _cached = row;
        _logger.LogInformation(
            "Installation Registered InstallationId={InstallationId} UserId={UserId} CompanyId={CompanyId}",
            row.InstallationId, userId, companyId);
    }
}
