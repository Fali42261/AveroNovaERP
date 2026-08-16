using System.Collections.Generic;

namespace AveroNova.Domain.Entities
{
    public class User : BaseEntity
    {
        public string UserCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsActiveUser { get; set; }
        public string UserImg { get; set; } = string.Empty;

        public ICollection<UserRole> UserRoles { get; set; }
            = new List<UserRole>();

        public ICollection<UserCompany> UserCompanies { get; set; }
            = new List<UserCompany>();
    }
}
