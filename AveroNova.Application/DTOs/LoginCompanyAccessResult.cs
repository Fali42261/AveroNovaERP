namespace AveroNova.Application.DTOs
{
    public sealed class LoginCompanyAccessResult
    {
        public bool IsAllowed { get; init; }
        public Guid? CompanyId { get; init; }
        public string? Message { get; init; }

        public static LoginCompanyAccessResult Allow(Guid companyId) => new()
        {
            IsAllowed = true,
            CompanyId = companyId
        };

        public static LoginCompanyAccessResult Deny(string message) => new()
        {
            IsAllowed = false,
            Message = message
        };
    }

    public sealed class TrialReminderInfo
    {
        public Guid CompanyId { get; init; }
        public DateTime EndDate { get; init; }
        public bool IsDue { get; init; }
    }
}
