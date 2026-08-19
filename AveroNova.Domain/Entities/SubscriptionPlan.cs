namespace AveroNova.Domain.Entities
{
    public class SubscriptionPlan : BaseEntity
    {
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public int DurationInDays { get; set; }

        public bool IsTrialPlan { get; set; }

        public bool IsCustomerAvailable { get; set; }

        public bool IsActive { get; set; }

        public int SortOrder { get; set; }

        public decimal Price { get; set; }

        public ICollection<SubscriptionPlanFeature> Features { get; set; }
            = new List<SubscriptionPlanFeature>();

        public ICollection<Subscription> Subscriptions { get; set; }
            = new List<Subscription>();
    }
}
