namespace AveroNova.Domain.Enums
{
    public enum SubscriptionStatus
    {
        Active = 1,
        Expired = 2,
        Suspended = 3,
        Cancelled = 4
    }

    /// <summary>
    /// Legacy duration values stored on Subscriptions.Plan.
    /// Current plan identity lives on SubscriptionPlans via PlanId.
    /// </summary>
    public enum SubscriptionDuration
    {
        Trial = 7,
        FifteenDays = 15,
        ThirtyDays = 30,
        NinetyDays = 90
    }

    public enum SubscriptionType
    {
        Trial = 1,
        Paid = 2
    }
}
