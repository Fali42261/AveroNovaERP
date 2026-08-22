using AveroNova.Application.Navigation;
using AveroNova.Application.Subscriptions;
using AveroNova.Domain.Constants;

namespace AveroNova.Application.DTOs;

public sealed class AuthorizationSnapshot
{
    public Guid UserId { get; init; }

    public Guid CompanyId { get; init; }

    public bool IsMember { get; init; }

    public bool IsSubscriptionActive { get; init; }

    public bool IsSubscriptionExpired { get; init; }

    public IReadOnlyList<string> EnabledModules { get; init; } = [];

    public IReadOnlySet<string> Permissions { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<NavigationMenuNode> Menus { get; init; } = [];

    public string? RestrictionReason { get; init; }

    public AccessDecision AuthorizeModule(string moduleKey)
    {
        var required = SubscriptionModulePermissions.RequiredAny(moduleKey);
        return Authorize(moduleKey, required);
    }

    public AccessDecision AuthorizeFeature(string moduleKey, string permissionName)
        => Authorize(moduleKey, string.IsNullOrWhiteSpace(permissionName) ? [] : [permissionName]);

    private AccessDecision Authorize(string moduleKey, IReadOnlyList<string> requiredAnyPermission)
    {
        if (CompanyId == Guid.Empty)
            return AccessDecision.Deny(CompanyId, moduleKey, SubscriptionMessages.CompanyContextRequired);

        if (!IsMember)
            return AccessDecision.Deny(CompanyId, moduleKey, SubscriptionMessages.UserNotInCompany);

        if (IsSubscriptionExpired || !IsSubscriptionActive)
        {
            return AccessDecision.Deny(
                CompanyId,
                moduleKey,
                RestrictionReason ?? SubscriptionMessages.FreeTrialExpiredAccess,
                expired: true);
        }

        if (!EnabledModules.Contains(moduleKey, StringComparer.OrdinalIgnoreCase))
            return AccessDecision.Deny(CompanyId, moduleKey, SubscriptionMessages.ModuleNotIncluded);

        if (requiredAnyPermission.Count > 0
            && !requiredAnyPermission.Any(p => HasPermission(p)))
        {
            return AccessDecision.Deny(CompanyId, moduleKey, SubscriptionMessages.PermissionDenied);
        }

        return AccessDecision.Allow(CompanyId, moduleKey);
    }

    private bool HasPermission(string permissionName)
        => PermissionNames.Grants(Permissions, permissionName);
}
