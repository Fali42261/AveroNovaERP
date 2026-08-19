using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Services
{
    public static class FreeTrialSubscriptionFactory
    {
        public const int DefaultDurationDays = 15;

        public static Subscription Create(Guid companyId, SubscriptionPlan plan, DateTime utcNow)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            var duration = plan.DurationInDays > 0 ? plan.DurationInDays : DefaultDurationDays;
            var start = utcNow;
            var end = start.AddDays(duration);

            return new Subscription
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                PlanId = plan.Id,
                PlanName = string.IsNullOrWhiteSpace(plan.Name) ? "Free Trial" : plan.Name,
                Price = 0m,
                DurationInDays = duration,
                StartDate = start,
                ExpiryDate = end,
                TrialStartDate = start,
                TrialEndDate = end,
                IsTrial = true,
                IsActive = true,
                AutoRenew = false,
                IsSubscription = true,
                SubscriptionType = SubscriptionType.Trial,
                Status = SubscriptionStatus.Active,
                Plan = SubscriptionDuration.FifteenDays,
                CreatedAt = utcNow,
                IsDeleted = false
            };
        }

        public static bool IsFreeTrialPlan(SubscriptionPlan plan)
            => plan.IsTrialPlan
               || string.Equals(plan.Code, SubscriptionPlanCodes.FreeTrial, StringComparison.OrdinalIgnoreCase);
    }
}
