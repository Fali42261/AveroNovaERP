namespace AveroNova.Domain.Entities
{
    public class SubscriptionPlanFeature : BaseEntity
    {
        public Guid PlanId { get; set; }

        public string ModuleKey { get; set; } = string.Empty;

        public string ModuleName { get; set; } = string.Empty;

        public bool IsEnabled { get; set; }

        public SubscriptionPlan Plan { get; set; } = null!;
    }
}
