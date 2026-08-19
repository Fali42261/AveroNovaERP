using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Services
{
    public static class SubscriptionStatusEvaluator
    {
        public static SubscriptionStatus Evaluate(Subscription? subscription, DateTime utcNow)
        {
            if (subscription == null || subscription.IsDeleted)
                return SubscriptionStatus.Expired;

            if (subscription.Status == SubscriptionStatus.Cancelled)
                return SubscriptionStatus.Cancelled;

            if (subscription.Status == SubscriptionStatus.Suspended)
                return SubscriptionStatus.Suspended;

            if (utcNow.Date > subscription.ExpiryDate.Date)
                return SubscriptionStatus.Expired;

            return SubscriptionStatus.Active;
        }

        public static bool IsEffectivelyActive(Subscription? subscription, DateTime utcNow)
            => Evaluate(subscription, utcNow) == SubscriptionStatus.Active;

        public static bool IsExpired(Subscription? subscription, DateTime utcNow)
            => Evaluate(subscription, utcNow) == SubscriptionStatus.Expired;

        public static void ApplyEvaluatedState(Subscription subscription, DateTime utcNow)
        {
            var evaluated = Evaluate(subscription, utcNow);
            subscription.Status = evaluated;
            subscription.IsActive = evaluated == SubscriptionStatus.Active;
            subscription.UpdatedAt = utcNow;
        }
    }
}
