using AveroNova.Application.DTOs.License;
using AveroNova.Domain.Enums;

namespace AveroNova.App.UI.Services.Interfaces;

public enum LicenseBootstrapStatus
{
    Ready = 0,
    NeedsInternet = 1,
    Failed = 2,
    Blocked = 3
}

public sealed class LicenseAccessState
{
    public bool AllowsAccess { get; init; }
    public bool NeedsFirstActivation { get; init; }
    public LicenseStatus Status { get; init; } = LicenseStatus.Trial;
    public string Plan { get; init; } = "Starter";
    public bool IsTrial { get; init; }
    public DateTime? TrialStartDateUtc { get; init; }
    public DateTime? TrialEndDateUtc { get; init; }
    public int RemainingTrialDays { get; init; }
    public DateTime? LastKnownServerTimeUtc { get; init; }
    public string? Message { get; init; }
}

public interface ILicenseService
{
    Task<LicenseBootstrapStatus> EnsureActivatedAsync(CancellationToken cancellationToken = default);
    Task<LicenseAccessState> GetAccessStateAsync(CancellationToken cancellationToken = default);
    Task ValidateOnlineIfPossibleAsync(CancellationToken cancellationToken = default);
    Task SyncOnlineIfPossibleAsync(CancellationToken cancellationToken = default);
    Task<LicenseStatusResponse?> GetCachedStatusAsync(CancellationToken cancellationToken = default);
}
