using AveroNova.Application.DTOs;
using AveroNova.Application.Navigation;

namespace AveroNova.Application.Interfaces
{
    public interface IAccessControlService
    {
        Task<AccessDecision> AuthorizeAsync(
            Guid userId,
            Guid companyId,
            string moduleKey,
            CancellationToken cancellationToken = default);

        Task<AccessDecision> AuthorizeFeatureAsync(
            Guid userId,
            Guid companyId,
            string moduleKey,
            string permissionName,
            CancellationToken cancellationToken = default);

        Task<AuthorizationSnapshot> GetSnapshotAsync(
            Guid userId,
            Guid companyId,
            CancellationToken cancellationToken = default);

        Task<bool> UserBelongsToCompanyAsync(
            Guid userId,
            Guid companyId,
            CancellationToken cancellationToken = default);
    }
}
