namespace AveroNova.Domain.Entities;

/// <summary>
/// Server-side device/session record for refresh-token binding (Phase 3 will issue tokens).
/// Stores only hashed refresh tokens — never raw tokens.
/// </summary>
public class DeviceSession : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string RefreshTokenHash { get; set; } = string.Empty;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? TokenFamilyId { get; set; }

    public User User { get; set; } = null!;
    public Company Company { get; set; } = null!;
}
