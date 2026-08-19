using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.Domain.Entities
{
    public class UserRole: BaseEntity
    {
        public Guid UserId { get; set; }

        public User? User { get; set; }

        public Guid RoleId { get; set; }

        public Role? Role { get; set; }

        /// <summary>
        /// Company this role assignment applies to.
        /// Null only for legacy rows created before company-scoped roles.
        /// </summary>
        public Guid? CompanyId { get; set; }

        public Company? Company { get; set; }
    }
}
