using AveroNova.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.Domain.Entities
{
    public class Subscription : BaseEntity
    {
        public Guid CompanyId { get; set; }

        public string PlanName { get; set; } = string.Empty;

        public decimal Price { get; set; }
        public int DurationInDays { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime ExpiryDate { get; set; }

        public bool IsSubscription { get; set; }

        public SubscriptionStatus Status { get; set; }

        public SubscriptionPlan Plan { get; set; }

        public Company Company { get; set; } = null!;
    }
}
