using AveroNova.Domain.Entities;

namespace AveroNova.Application.Interfaces.Repositories
{
    public interface ISubscriptionAccessRepository
    {
        Task<Subscription?> GetCurrentForCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

        Task<SubscriptionPlan?> GetPlanByIdAsync(Guid planId, CancellationToken cancellationToken = default);

        Task<SubscriptionPlan?> GetPlanByCodeAsync(string code, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetEnabledModuleKeysAsync(Guid planId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SubscriptionPlan>> GetCustomerAvailablePlansAsync(CancellationToken cancellationToken = default);

        Task<bool> UserBelongsToCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetUserPermissionNamesAsync(
            Guid userId,
            Guid companyId,
            CancellationToken cancellationToken = default);

        Task AddSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken = default);

        Task AddUserCompanyAsync(UserCompany userCompany, CancellationToken cancellationToken = default);

        Task UpdateSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken = default);

        Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Guid>> GetCompanyIdsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
