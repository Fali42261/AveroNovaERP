using AveroNova.Domain.Constants;
using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Entities;

/// <summary>
/// Server-authoritative product license / trial. Does not store authentication secrets.
/// </summary>
public class License : BaseEntity
{
    public Guid? UserId { get; set; }
    public Guid? CompanyId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Plan { get; set; } = LicenseConstants.StarterPlan;
    public LicenseStatus Status { get; set; } = LicenseStatus.Trial;
    public bool IsTrial { get; set; } = true;
    public DateTime TrialStartDateUtc { get; set; }
    public DateTime TrialEndDateUtc { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime? ExpiryDateUtc { get; set; }
    public DateTime? LastValidatedAtUtc { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }

    public User? User { get; set; }
    public Company? Company { get; set; }

    public static License StartStarterTrial(
        string deviceId,
        DateTime utcNow,
        Guid? userId = null,
        Guid? companyId = null,
        Guid? licenseId = null)
    {
        var start = utcNow.Kind == DateTimeKind.Utc ? utcNow : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var end = start.AddDays(LicenseConstants.TrialDays);
        return new License
        {
            Id = licenseId is Guid id && id != Guid.Empty ? id : Guid.NewGuid(),
            DeviceId = deviceId,
            UserId = userId,
            CompanyId = companyId,
            Plan = LicenseConstants.StarterPlan,
            Status = LicenseStatus.Trial,
            IsTrial = true,
            TrialStartDateUtc = start,
            TrialEndDateUtc = end,
            StartDateUtc = start,
            ExpiryDateUtc = end,
            CreatedAt = start,
            LastValidatedAtUtc = start,
            LastSyncedAtUtc = start,
            SyncStatus = RecordSyncStatus.Synced
        };
    }
}
