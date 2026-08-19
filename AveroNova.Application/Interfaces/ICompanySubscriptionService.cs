using AveroNova.Application.DTOs;
using AveroNova.Domain.Entities;

namespace AveroNova.Application.Interfaces
{
    public interface ICompanySubscriptionService
    {
        Task<CompanySubscriptionSnapshot?> GetCurrentAsync(Guid companyId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetEnabledModulesAsync(Guid companyId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SubscriptionPlan>> GetCustomerAvailablePlansAsync(CancellationToken cancellationToken = default);

        Task<Subscription> CreateFreeTrialAsync(Guid companyId, CancellationToken cancellationToken = default);

        Task<LoginCompanyAccessResult> ResolveLoginCompanyAsync(
            Guid userId,
            Guid? preferredCompanyId,
            CancellationToken cancellationToken = default);

        Task<TrialReminderInfo?> GetTrialReminderAsync(
            Guid companyId,
            CancellationToken cancellationToken = default);
    }
}
