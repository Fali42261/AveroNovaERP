using AveroNova.Application.Interfaces;
using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;

namespace AveroNova.Application.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IPlanRepository _planRepository;

        public SubscriptionService(
            ISubscriptionRepository subscriptionRepository,
            IPlanRepository planRepository)
        {
            _subscriptionRepository = subscriptionRepository;
            _planRepository = planRepository;
        }

        public async Task AddAsync(Subscription subscription)
        {
            await _subscriptionRepository.AddAsync(subscription);
        }

        public async Task<Subscription> CreateFromPlanAsync(Guid companyId, Guid planId, bool isTrial)
        {
            var plan = await _planRepository.GetByIdAsync(planId)
                ?? throw new InvalidOperationException($"Plan '{planId}' was not found.");

            var subscription = Subscription.StartFromPlan(
                companyId,
                plan,
                DateTime.UtcNow,
                isTrial);

            await _subscriptionRepository.AddAsync(subscription);
            return subscription;
        }

        public async Task<(bool IsActive, string? Reason)> CheckSubscriptionStatusAsync(Guid companyId)
        {
            var subscription = await _subscriptionRepository.GetByCompanyIdAsync(companyId);

            if (subscription == null)
            {
                return (false, "No subscription found for company");
            }

            if (subscription.Status == SubscriptionStatus.Expired)
            {
                return (false, "Subscription has expired");
            }

            if (subscription.Status == SubscriptionStatus.Suspended)
            {
                return (false, "Subscription is suspended");
            }

            if (subscription.Status == SubscriptionStatus.Cancelled)
            {
                return (false, "Subscription has been cancelled");
            }

            if (DateTime.UtcNow > subscription.EndDate)
            {
                subscription.Status = SubscriptionStatus.Expired;
                subscription.UpdatedAt = DateTime.UtcNow;
                await _subscriptionRepository.UpdateAsync(subscription);
                return (false, "Subscription period has ended");
            }

            return (true, null);
        }

        public async Task DeleteAsync(Guid id)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(id);
            if (subscription != null)
            {
                await _subscriptionRepository.DeleteAsync(subscription);
            }
        }

        public async Task<List<Subscription>> GetAllAsync()
        {
            return await _subscriptionRepository.GetAllAsync();
        }

        public async Task<Subscription?> GetByCompanyIdAsync(Guid companyId)
        {
            return await _subscriptionRepository.GetByCompanyIdAsync(companyId);
        }

        public async Task<Subscription?> GetByIdAsync(Guid id)
        {
            return await _subscriptionRepository.GetByIdAsync(id);
        }

        public async Task UpdateAsync(Subscription subscription)
        {
            subscription.UpdatedAt = DateTime.UtcNow;
            await _subscriptionRepository.UpdateAsync(subscription);
        }
    }
}
