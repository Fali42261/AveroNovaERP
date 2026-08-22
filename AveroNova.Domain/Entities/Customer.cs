using System;

namespace AveroNova.Domain.Entities
{
    public class Customer : BaseEntity
    {
        public Guid CompanyId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string MobileNumber { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        public string State { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public string PinCode { get; set; } = string.Empty;

        public string TaxNumber { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Stored as <c>AveroNova.App.UI.Models.CustomerStatus</c> integer:
        /// 0 Active, 1 Inactive, 2 Blocked.
        /// </summary>
        public int Status { get; set; }

        public Company Company { get; set; } = null!;
    }
}
