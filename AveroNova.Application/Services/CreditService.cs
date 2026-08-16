using AveroNova.Application.Interfaces;
using AveroNova.Application.Interfaces.Repositories;

namespace AveroNova.Application.Services
{
    public class CreditService : ICreditService
    {
        private readonly ISubscriptionRepository _subscriptionRepository;

        public CreditService(ISubscriptionRepository subscriptionRepository)
        {
            _subscriptionRepository = subscriptionRepository;
        }

        public async Task<bool> CanConsumeCreditsAsync(Guid companyId, int creditsToConsume)
        {
            var remaining = await GetRemainingCreditsAsync(companyId);
            return remaining >= creditsToConsume && creditsToConsume > 0;
        }

        public async Task<(bool Success, string? Error)> ConsumeCreditsAsync(Guid companyId, int creditsToConsume)
        {
            if (creditsToConsume <= 0)
            {
                return (false, "Credits to consume must be greater than zero");
            }

            var subscription = await _subscriptionRepository.GetByCompanyIdAsync(companyId);

            if (subscription == null)
            {
                return (false, "Subscription not found for company");
            }

            var remaining = subscription.CreditLimit - subscription.CreditsUsed;

            if (remaining < creditsToConsume)
            {
                return (false, $"Insufficient credits. Available: {remaining}, Requested: {creditsToConsume}");
            }

            subscription.CreditsUsed += creditsToConsume;
            await _subscriptionRepository.UpdateAsync(subscription);

            return (true, null);
        }

        public async Task<int> GetCreditLimitAsync(Guid companyId)
        {
            var subscription = await _subscriptionRepository.GetByCompanyIdAsync(companyId);
            return subscription?.CreditLimit ?? 0;
        }

        public async Task<int> GetCreditsUsedAsync(Guid companyId)
        {
            var subscription = await _subscriptionRepository.GetByCompanyIdAsync(companyId);
            return subscription?.CreditsUsed ?? 0;
        }

        public async Task<int> GetRemainingCreditsAsync(Guid companyId)
        {
            var subscription = await _subscriptionRepository.GetByCompanyIdAsync(companyId);

            if (subscription == null)
            {
                return 0;
            }

            var remaining = subscription.CreditLimit - subscription.CreditsUsed;
            return remaining < 0 ? 0 : remaining;
        }

        public async Task<(bool Success, string? Error)> RefundCreditsAsync(Guid companyId, int creditsToRefund)
        {
            if (creditsToRefund <= 0)
            {
                return (false, "Credits to refund must be greater than zero");
            }

            var subscription = await _subscriptionRepository.GetByCompanyIdAsync(companyId);

            if (subscription == null)
            {
                return (false, "Subscription not found for company");
            }

            var newUsed = subscription.CreditsUsed - creditsToRefund;
            subscription.CreditsUsed = newUsed < 0 ? 0 : newUsed;
            await _subscriptionRepository.UpdateAsync(subscription);

            return (true, null);
        }
    }
}
