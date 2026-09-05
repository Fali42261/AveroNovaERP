namespace AveroNova.App.UI.Services.Security;

public interface ILocalCredentialStore
{
    Task SetPasswordHashAsync(Guid userId, string email, string passwordHash);
    Task<string?> GetPasswordHashAsync(Guid userId);
    Task<Guid?> FindUserIdByEmailAsync(string email);
}

public interface ILicenseAnchorStore
{
    Task SaveAsync(LicenseAnchor anchor);
    Task<LicenseAnchor?> LoadAsync();
}

public sealed class LicenseAnchor
{
    public string DeviceId { get; set; } = string.Empty;
    public Guid LicenseId { get; set; }
    public string Plan { get; set; } = "Starter";
    public int Status { get; set; }
    public bool IsTrial { get; set; } = true;
    public DateTime TrialStartDateUtc { get; set; }
    public DateTime TrialEndDateUtc { get; set; }
    public DateTime? ExpiryDateUtc { get; set; }
    public DateTime LastKnownTrustedTimeUtc { get; set; }
    public DateTime? LastKnownServerTimeUtc { get; set; }
    public bool IsServerAuthoritative { get; set; }
}
