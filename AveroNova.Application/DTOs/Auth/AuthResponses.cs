namespace AveroNova.Application.DTOs.Auth;

public sealed class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public DateTime AccessTokenExpiresAtUtc { get; set; }

    public AuthUserDto User { get; set; } = new();
    public AuthCompanyDto CurrentCompany { get; set; } = new();
    public IReadOnlyList<AuthCompanyDto> Companies { get; set; } = [];
    public IReadOnlyList<string> Roles { get; set; } = [];
    public IReadOnlyList<string> Permissions { get; set; } = [];
    public AuthSessionDto Session { get; set; } = new();
}

public sealed class RegisterResponse
{
    public bool Success { get; set; }
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SubscriptionId { get; set; }
    public string Plan { get; set; } = string.Empty;
    public DateTime TrialStartDate { get; set; }
    public DateTime TrialEndDate { get; set; }
}

public sealed class AuthUserDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class AuthCompanyDto
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsOwner { get; set; }
}

public sealed class AuthSessionDto
{
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime OfflineSessionExpiresAtUtc { get; set; }
}

public sealed class MeResponse
{
    public AuthUserDto User { get; set; } = new();
    public AuthCompanyDto CurrentCompany { get; set; } = new();
    public IReadOnlyList<AuthCompanyDto> Companies { get; set; } = [];
    public IReadOnlyList<string> Roles { get; set; } = [];
    public IReadOnlyList<string> Permissions { get; set; } = [];
}
