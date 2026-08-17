using System.Text.Json.Serialization;
using AveroNova.Domain.Enums;

namespace AveroNova.Application.DTOs.License;

public sealed class LicenseInitializeRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public Guid? InstallationId { get; set; }
    public Guid? ClientLicenseId { get; set; }
    public DateTime? ClientTrialStartDateUtc { get; set; }
    public DateTime? ClientTrialEndDateUtc { get; set; }
}

public sealed class LicenseValidateRequest
{
    public string DeviceId { get; set; } = string.Empty;
}

public sealed class LicenseSyncRequest
{
    public string DeviceId { get; set; } = string.Empty;
}

public sealed class LicenseStatusResponse
{
    public Guid LicenseId { get; set; }
    public string Plan { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LicenseStatus Status { get; set; }

    public bool IsTrial { get; set; }
    public DateTime TrialStartDateUtc { get; set; }
    public DateTime TrialEndDateUtc { get; set; }
    public DateTime? ExpiryDateUtc { get; set; }
    public DateTime ServerTimeUtc { get; set; }
    public DateTime? LastValidatedAtUtc { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
}
