namespace AveroNova.App.UI.Models;

// ═══════════════════════════════════════════════════════════════
//  AVERONOVA ERP — USER MODEL
// ═══════════════════════════════════════════════════════════════

public class UserModel : BaseModel
{
    public string   Name           { get; set; } = string.Empty;
    public string   Email          { get; set; } = string.Empty;
    public string   Phone          { get; set; } = string.Empty;
    public string   Role           { get; set; } = string.Empty;
    public string   AvatarInitials { get; set; } = string.Empty;
    public string?  AvatarUrl      { get; set; }
    public Guid?    CompanyId      { get; set; }
    public string   CompanyName    { get; set; } = string.Empty;
    public UserStatus Status       { get; set; } = UserStatus.Active;
    public DateTime? LastLoginAt   { get; set; }
    public string Notes { get; set; } = string.Empty;
    public Guid? RoleId { get; set; }

    // Computed
    public string StatusLabel => Status switch
    {
        UserStatus.Active    => "Active",
        UserStatus.Inactive  => "Inactive",
        UserStatus.Suspended => "Suspended",
        _                    => "Unknown"
    };

    public string LastLoginDisplay => LastLoginAt.HasValue
        ? LastLoginAt.Value.ToString("dd MMM yyyy, hh:mm tt")
        : "Never";
}
