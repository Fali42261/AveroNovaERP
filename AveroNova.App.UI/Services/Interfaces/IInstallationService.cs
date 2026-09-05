using AveroNova.App.UI.Data;

namespace AveroNova.App.UI.Services.Interfaces;

public interface IInstallationService
{
    Guid InstallationId { get; }
    string DeviceId { get; }
    bool IsRegistered { get; }
    LocalInstallationStatus Status { get; }

    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
    Task<LocalInstallationEntity> GetAsync(CancellationToken cancellationToken = default);
    Task MarkRegisteredAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);
    bool CanCreateAccount { get; }
}

public interface ILocalSessionPolicy
{
    Task<bool> HasValidOfflineSessionAsync(CancellationToken cancellationToken = default);
}
