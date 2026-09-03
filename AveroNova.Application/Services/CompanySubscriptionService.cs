using AveroNova.Application.DTOs;
using AveroNova.Application.Interfaces;
using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Domain.Services;

namespace AveroNova.Application.Services
{
    public sealed class CompanySubscriptionService : ICompanySubscriptionService
    {
        private readonly ISubscriptionAccessRepository _repository;

        public CompanySubscriptionService(ISubscriptionAccessRepository repository)
        {
            _repository = repository;
        }

        public async Task<CompanySubscriptionSnapshot?> GetCurrentAsync(
            Guid companyId,
            CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty)
                return null;

            var subscription = await _repository.GetCurrentForCompanyAsync(companyId, cancellationToken);
            if (subscription == null)
                return null;

            var utcNow = DateTime.UtcNow;
            var evaluated = SubscriptionStatusEvaluator.Evaluate(subscription, utcNow);
            if (subscription.Status != evaluated || subscription.IsActive != (evaluated == SubscriptionStatus.Active))
            {
                SubscriptionStatusEvaluator.ApplyEvaluatedState(subscription, utcNow);
                await _repository.UpdateSubscriptionAsync(subscription, cancellationToken);
            }

            var plan = subscription.PlanId.HasValue
                ? await _repository.GetPlanByIdAsync(subscription.PlanId.Value, cancellationToken)
                : null;

            IReadOnlyList<string> modules = [];
            if (subscription.PlanId.HasValue)
                modules = await _repository.GetEnabledModuleKeysAsync(subscription.PlanId.Value, cancellationToken);

            return new CompanySubscriptionSnapshot
            {
                SubscriptionId = subscription.Id,
                CompanyId = companyId,
                PlanId = subscription.PlanId,
                PlanCode = plan?.Code ?? string.Empty,
                PlanName = string.IsNullOrWhiteSpace(subscription.PlanName) ? (plan?.Name ?? string.Empty) : subscription.PlanName,
                SubscriptionType = subscription.SubscriptionType,
                StoredStatus = subscription.Status,
                EffectiveStatus = evaluated,
                StartDate = subscription.StartDate,
                EndDate = subscription.ExpiryDate,
                TrialStartDate = subscription.TrialStartDate,
                TrialEndDate = subscription.TrialEndDate,
                IsTrial = subscription.IsTrial,
                IsActive = evaluated == SubscriptionStatus.Active,
                AutoRenew = subscription.AutoRenew,
                EnabledModules = modules
            };
        }

        public async Task<IReadOnlyList<string>> GetEnabledModulesAsync(
            Guid companyId,
            CancellationToken cancellationToken = default)
        {
            var current = await GetCurrentAsync(companyId, cancellationToken);
            return current?.EnabledModules ?? [];
        }

        public Task<IReadOnlyList<SubscriptionPlan>> GetCustomerAvailablePlansAsync(
            CancellationToken cancellationToken = default)
            => _repository.GetCustomerAvailablePlansAsync(cancellationToken);

        public async Task<Subscription> CreateFreeTrialAsync(
            Guid companyId,
            CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty)
                throw new ArgumentException("Company is required.", nameof(companyId));

            if (!await _repository.CompanyExistsAsync(companyId, cancellationToken))
                throw new InvalidOperationException("Company was not found.");

            var existing = await _repository.GetCurrentForCompanyAsync(companyId, cancellationToken);
            if (existing != null)
                return existing;

            var plan = await _repository.GetPlanByCodeAsync(SubscriptionPlanCodes.FreeTrial, cancellationToken);
            if (plan == null)
                throw new InvalidOperationException("Free Trial plan is missing from the subscription catalog.");

            var subscription = FreeTrialSubscriptionFactory.Create(companyId, plan, DateTime.UtcNow);
            await _repository.AddSubscriptionAsync(subscription, cancellationToken);
            return subscription;
        }

        public async Task<LoginCompanyAccessResult> ResolveLoginCompanyAsync(
            Guid userId,
            Guid? preferredCompanyId,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
                return LoginCompanyAccessResult.Deny("User not found.");

            var companyIds = await _repository.GetCompanyIdsForUserAsync(userId, cancellationToken);
            if (companyIds.Count == 0)
                return LoginCompanyAccessResult.Deny("No company found for this user.");

            // Return preferred company if user belongs to it, regardless of subscription status
            if (preferredCompanyId is Guid preferred && preferred != Guid.Empty && companyIds.Contains(preferred))
                return LoginCompanyAccessResult.Allow(preferred);

            // Return first available company, regardless of subscription status
            // Subscription restrictions are applied during feature access, not login
            return LoginCompanyAccessResult.Allow(companyIds[0]);
        }

        public async Task<TrialReminderInfo?> GetTrialReminderAsync(
            Guid companyId,
            CancellationToken cancellationToken = default)
        {
            var current = await GetCurrentAsync(companyId, cancellationToken);
            if (current == null || !current.IsTrial || !current.IsActive)
                return null;

            if (!TrialReminderEvaluator.IsDueTomorrow(current.EndDate, DateTime.UtcNow))
                return null;

            return new TrialReminderInfo
            {
                CompanyId = companyId,
                EndDate = current.EndDate,
                IsDue = true
            };
        }
    }
}
