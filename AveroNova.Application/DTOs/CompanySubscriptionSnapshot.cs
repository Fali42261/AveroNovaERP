using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;

namespace AveroNova.Application.DTOs
{
    public sealed class CompanySubscriptionSnapshot
    {
        public Guid SubscriptionId { get; init; }
        public Guid CompanyId { get; init; }
        public Guid? PlanId { get; init; }
        public string PlanCode { get; init; } = string.Empty;
        public string PlanName { get; init; } = string.Empty;
        public SubscriptionType SubscriptionType { get; init; }
        public SubscriptionStatus StoredStatus { get; init; }
        public SubscriptionStatus EffectiveStatus { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public DateTime? TrialStartDate { get; init; }
        public DateTime? TrialEndDate { get; init; }
        public bool IsTrial { get; init; }
        public bool IsActive { get; init; }
        public bool AutoRenew { get; init; }
        public IReadOnlyList<string> EnabledModules { get; init; } = [];

        public bool IsExpired => EffectiveStatus == SubscriptionStatus.Expired;

        public string RestrictionMessage =>
            IsTrial && IsExpired
                ? Domain.Constants.SubscriptionMessages.FreeTrialExpiredAccess
                : Domain.Constants.SubscriptionMessages.ModuleNotIncluded;
    }

    public sealed class AccessDecision
    {
        public bool IsAllowed { get; init; }
        public string? Reason { get; init; }
        public bool IsSubscriptionExpired { get; init; }
        public string ModuleKey { get; init; } = string.Empty;
        public Guid CompanyId { get; init; }

        public static AccessDecision Allow(Guid companyId, string moduleKey) => new()
        {
            IsAllowed = true,
            ModuleKey = moduleKey,
            CompanyId = companyId
        };

        public static AccessDecision Deny(Guid companyId, string moduleKey, string reason, bool expired = false) => new()
        {
            IsAllowed = false,
            Reason = reason,
            IsSubscriptionExpired = expired,
            ModuleKey = moduleKey,
            CompanyId = companyId
        };
    }
}
