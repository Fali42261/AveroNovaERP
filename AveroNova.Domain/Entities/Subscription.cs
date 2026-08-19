using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Entities
{
    public class Subscription : BaseEntity
    {
        public Guid CompanyId { get; set; }

        public Guid? PlanId { get; set; }

        public string PlanName { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public int DurationInDays { get; set; }

        public DateTime StartDate { get; set; }

        /// <summary>
        /// Subscription end date. Existing column name is ExpiryDate.
        /// </summary>
        public DateTime ExpiryDate { get; set; }

        public DateTime? TrialStartDate { get; set; }

        public DateTime? TrialEndDate { get; set; }

        public bool IsTrial { get; set; }

        public bool IsActive { get; set; }

        public bool AutoRenew { get; set; }

        public bool IsSubscription { get; set; }

        public SubscriptionType SubscriptionType { get; set; }

        public SubscriptionStatus Status { get; set; }

        /// <summary>
        /// Legacy duration code. Plan identity is PlanId → SubscriptionPlans.
        /// </summary>
        public SubscriptionDuration Plan { get; set; }

        public Company Company { get; set; } = null!;

        public SubscriptionPlan? SubscriptionPlan { get; set; }
    }
}
