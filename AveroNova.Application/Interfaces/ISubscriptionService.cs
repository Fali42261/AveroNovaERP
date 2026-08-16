using AveroNova.Domain.Entities;

namespace AveroNova.Application.Interfaces
{
    public interface ISubscriptionService
    {
        Task<Subscription?> GetByCompanyIdAsync(Guid companyId);
        Task<Subscription?> GetByIdAsync(Guid id);
        Task<List<Subscription>> GetAllAsync();
        Task AddAsync(Subscription subscription);
        Task UpdateAsync(Subscription subscription);
        Task DeleteAsync(Guid id);
        Task<(bool IsActive, string? Reason)> CheckSubscriptionStatusAsync(Guid companyId);
        Task<Subscription> CreateFromPlanAsync(Guid companyId, Guid planId, bool isTrial);
    }
}
