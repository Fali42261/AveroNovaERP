using System;
using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Entities
{
    public class Subscription : BaseEntity
    {
        public Guid CompanyId { get; set; }
        public Guid PlanId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsTrial { get; set; }
        public int CreditLimit { get; set; }
        public int CreditsUsed { get; set; }
        public SubscriptionStatus Status { get; set; }

        public int RemainingCredits =>
            CreditLimit - CreditsUsed < 0 ? 0 : CreditLimit - CreditsUsed;

        public Plan Plan { get; set; } = null!;
        public Company Company { get; set; } = null!;

        public static Subscription StartFromPlan(
            Guid companyId,
            Plan plan,
            DateTime startDateUtc,
            bool isTrial)
        {
            ArgumentNullException.ThrowIfNull(plan);

            return new Subscription
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                PlanId = plan.Id,
                StartDate = startDateUtc,
                EndDate = plan.CalculatePeriodEndDate(startDateUtc),
                IsTrial = isTrial,
                CreditLimit = plan.CreditLimit,
                CreditsUsed = 0,
                Status = SubscriptionStatus.Active,
                CreatedAt = startDateUtc
            };
        }
    }
}
