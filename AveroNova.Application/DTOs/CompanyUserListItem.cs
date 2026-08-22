using AveroNova.Domain.Entities;

namespace AveroNova.Application.DTOs
{
    public sealed class CompanyUserListItem
    {
        public required User User { get; init; }
        public required UserCompany Membership { get; init; }
        public IReadOnlyList<string> RoleNames { get; init; } = [];
        public IReadOnlyList<Guid> RoleIds { get; init; } = [];
        public Guid? PrimaryRoleId => RoleIds.Count == 0 ? null : RoleIds[0];
    }
}
