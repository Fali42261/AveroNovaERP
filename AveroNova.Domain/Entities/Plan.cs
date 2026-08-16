using System;
using System.Collections.Generic;
using AveroNova.Domain.Constants;

namespace AveroNova.Domain.Entities
{
    public class Plan : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int TrialDays { get; set; }
        public int CreditLimit { get; set; }
        public decimal Price { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<Subscription> Subscriptions { get; set; }
            = new List<Subscription>();

        public DateTime CalculatePeriodEndDate(DateTime startDate)
        {
            return startDate.AddDays(TrialDays);
        }

        public static Plan CreateFreeTrialCatalog()
        {
            return new Plan
            {
                Id = Guid.NewGuid(),
                Name = PlanNames.FreeTrial,
                Description = "Default company free trial catalog plan.",
                TrialDays = 15,
                CreditLimit = 1000,
                Price = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
