namespace AveroNova.Application.DTOs.Auth;

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public Guid? CompanyId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
}

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
    public Guid? SessionId { get; set; }
    public string? DeviceId { get; set; }
}

public sealed class LogoutRequest
{
    public Guid? SessionId { get; set; }
    public string? RefreshToken { get; set; }
}

public sealed class RegisterRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public string CompanyEmail { get; set; } = string.Empty;
    public string CompanyMobile { get; set; } = string.Empty;
    public string? GstNumber { get; set; }
    public string? PanNumber { get; set; }
    public string? CompanyAddress { get; set; }
    public string? CompanyCity { get; set; }
    public string? CompanyState { get; set; }
    public string? CompanyCountry { get; set; }
    public string? CompanyPinCode { get; set; }

    public string Plan { get; set; } = "Starter";

    /// <summary>Client-generated installation identity (required for first-setup registration).</summary>
    public Guid InstallationId { get; set; }

    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;

    /// <summary>Optional client-generated stable IDs for offline→online sync idempotency.</summary>
    public Guid? ClientUserId { get; set; }
    public Guid? ClientCompanyId { get; set; }
    public Guid? ClientUserCompanyId { get; set; }
    public Guid? ClientSubscriptionId { get; set; }
}
