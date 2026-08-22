using AveroNova.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.Domain.Entities
{
    public class User:BaseEntity
    {
        public string UserCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string MobileNumber { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public bool IsActiveUser { get; set; }
        public string UserImg { get; set; } = string.Empty;

        // Navigation Properties
        public ICollection<UserRole> UserRoles { get; set; }
            = new List<UserRole>();
        
        public ICollection<Company> Companies { get; set; }
            = new List<Company>();

        public ICollection<UserCompany> UserCompanies { get; set; }
            = new List<UserCompany>();
    }
}
