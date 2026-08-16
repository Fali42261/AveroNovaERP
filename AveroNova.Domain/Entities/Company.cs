using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.Domain.Entities
{
    public class Company: BaseEntity
    {                
        public Guid UserId { get; set; }
        public string CompanyCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;

        public string OwnerName { get; set; } = string.Empty;

        public string GSTNumber { get; set; } = string.Empty;

        public string PANNumber { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string MobileNumber { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        public string State { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public string PinCode { get; set; } = string.Empty;

        public User User { get; set; } = null!;

        public ICollection<Subscription> Subscriptions { get; set; }
            = new List<Subscription>();

    }
}
