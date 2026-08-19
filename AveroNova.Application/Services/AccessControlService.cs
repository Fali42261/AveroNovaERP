using AveroNova.Application.DTOs;
using AveroNova.Application.Interfaces;
using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Application.Navigation;
using AveroNova.Domain.Constants;

namespace AveroNova.Application.Services
{
    public sealed class AccessControlService : IAccessControlService
    {
        private readonly ICompanySubscriptionService _subscriptions;
        private readonly ISubscriptionAccessRepository _repository;

        public AccessControlService(
            ICompanySubscriptionService subscriptions,
            ISubscriptionAccessRepository repository)
        {
            _subscriptions = subscriptions;
            _repository = repository;
        }

        public Task<bool> UserBelongsToCompanyAsync(
            Guid userId,
            Guid companyId,
            CancellationToken cancellationToken = default)
            => _repository.UserBelongsToCompanyAsync(userId, companyId, cancellationToken);

        public async Task<AccessDecision> AuthorizeAsync(
            Guid userId,
            Guid companyId,
            string moduleKey,
            CancellationToken cancellationToken = default)
        {
            var snapshot = await GetSnapshotAsync(userId, companyId, cancellationToken);
            return snapshot.AuthorizeModule(moduleKey);
        }

        public async Task<AccessDecision> AuthorizeFeatureAsync(
            Guid userId,
            Guid companyId,
            string moduleKey,
            string permissionName,
            CancellationToken cancellationToken = default)
        {
            var snapshot = await GetSnapshotAsync(userId, companyId, cancellationToken);
            return snapshot.AuthorizeFeature(moduleKey, permissionName);
        }

        public async Task<AuthorizationSnapshot> GetSnapshotAsync(
            Guid userId,
            Guid companyId,
            CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty)
            {
                return new AuthorizationSnapshot
                {
                    UserId = userId,
                    CompanyId = companyId,
                    RestrictionReason = SubscriptionMessages.CompanyContextRequired
                };
            }

            var isMember = userId != Guid.Empty
                && await _repository.UserBelongsToCompanyAsync(userId, companyId, cancellationToken);
            if (!isMember)
            {
                return new AuthorizationSnapshot
                {
                    UserId = userId,
                    CompanyId = companyId,
                    RestrictionReason = SubscriptionMessages.UserNotInCompany
                };
            }

            var subscription = await _subscriptions.GetCurrentAsync(companyId, cancellationToken);
            if (subscription == null || subscription.IsExpired || !subscription.IsActive)
            {
                var expiredMessage = subscription?.IsTrial == true
                    ? SubscriptionMessages.FreeTrialExpiredAccess
                    : SubscriptionMessages.ModuleNotIncluded;
                return new AuthorizationSnapshot
                {
                    UserId = userId,
                    CompanyId = companyId,
                    IsMember = true,
                    IsSubscriptionExpired = true,
                    RestrictionReason = expiredMessage
                };
            }

            var permissions = await _repository.GetUserPermissionNamesAsync(userId, companyId, cancellationToken);
            var permissionSet = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
            var menus = NavigationMenuCatalog.Build(permissionSet, subscription.EnabledModules);

            return new AuthorizationSnapshot
            {
                UserId = userId,
                CompanyId = companyId,
                IsMember = true,
                IsSubscriptionActive = true,
                EnabledModules = subscription.EnabledModules,
                Permissions = permissionSet,
                Menus = menus
            };
        }
    }
}
