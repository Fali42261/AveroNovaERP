using AveroNova.Application.DTOs;
using AveroNova.Domain.Entities;

namespace AveroNova.Application.Interfaces.Repositories
{
    public interface ICompanyUserRepository
    {
        Task<IReadOnlyList<CompanyUserListItem>> QueryAsync(
            Guid companyId,
            string? searchText,
            Guid? roleId,
            bool? isActive,
            CancellationToken cancellationToken = default);

        Task<CompanyUserListItem?> GetByIdAsync(
            Guid companyId,
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<bool> EmailExistsAsync(
            string email,
            Guid? excludeUserId,
            CancellationToken cancellationToken = default);

        Task<bool> IsOwnerAsync(
            Guid companyId,
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<bool> RoleIsAssignableAsync(
            Guid roleId,
            CancellationToken cancellationToken = default);

        Task CreateInCompanyAsync(
            User user,
            UserCompany membership,
            UserRole assignment,
            CancellationToken cancellationToken = default);

        Task UpdateInCompanyAsync(
            Guid companyId,
            User user,
            Guid? roleId,
            bool isActive,
            CancellationToken cancellationToken = default);

        Task<bool> SoftDeleteInCompanyAsync(
            Guid companyId,
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Role>> GetAssignableRolesAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Role>> GetRolesUsedInCompanyAsync(
            Guid companyId,
            CancellationToken cancellationToken = default);

        Task<Role?> GetRoleByIdAsync(Guid roleId, CancellationToken cancellationToken = default);

        Task<int> CountUsersWithRoleAsync(
            Guid companyId,
            Guid roleId,
            CancellationToken cancellationToken = default);

        Task<bool> SoftDeleteRoleAsync(
            Guid roleId,
            CancellationToken cancellationToken = default);
    }
}
