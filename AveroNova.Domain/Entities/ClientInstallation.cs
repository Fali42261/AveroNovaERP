using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Entities;

/// <summary>
/// Server record of a client software installation that completed first-time registration.
/// InstallationId is generated on the client and is distinct from DeviceId / UserId / CompanyId.
/// </summary>
public class ClientInstallation : BaseEntity
{
    public Guid InstallationId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Company Company { get; set; } = null!;
}
