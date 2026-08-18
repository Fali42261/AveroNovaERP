namespace AveroNova.App.UI.Models;

public sealed class RegistrationRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string GSTNumber { get; set; } = string.Empty;
    public string PANNumber { get; set; } = string.Empty;
    public string CompanyEmail { get; set; } = string.Empty;
    public string CompanyMobile { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PinCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PlanId { get; set; } = "starter";
    public string PlanName { get; set; } = "Starter";
}

public sealed class RegistrationResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public bool LocalAccountCreated { get; init; }
    public bool ServerSynced { get; init; }
    public Guid? UserId { get; init; }
    public Guid? CompanyId { get; init; }
    public Guid? SubscriptionId { get; init; }
    public Guid? RoleId { get; init; }

    public static RegistrationResult Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}
