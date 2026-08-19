using AveroNova.Application.DTOs;
using AveroNova.Application.Interfaces;
using AveroNova.Application.Navigation;
using AveroNova.App.UI.Services.Local;

namespace AveroNova.App.UI.SubscriptionAccess;

public sealed class CurrentAccessService
{
    private readonly IAccessControlService _access;
    private readonly object _gate = new();
    private AuthorizationSnapshot? _cache;
    private Guid _cachedUserId;
    private Guid _cachedCompanyId;

    public CurrentAccessService(IAccessControlService access)
    {
        _access = access;
    }

    public Task<AccessDecision> AuthorizeAsync(string moduleKey, CancellationToken cancellationToken = default)
        => AuthorizeFeatureAsync(moduleKey, permissionName: null, cancellationToken);

    public async Task<AccessDecision> AuthorizeFeatureAsync(
        string moduleKey,
        string? permissionName,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(permissionName)
            ? snapshot.AuthorizeModule(moduleKey)
            : snapshot.AuthorizeFeature(moduleKey, permissionName);
    }

    public async Task<AccessDecision> AuthorizeMenuAsync(string menuKey, CancellationToken cancellationToken = default)
    {
        var definition = NavigationMenuCatalog.Find(menuKey);
        if (definition == null)
            return AccessDecision.Deny(LocalSessionStore.CompanyId ?? Guid.Empty, menuKey, "Unknown menu.");

        return await AuthorizeFeatureAsync(definition.SubscriptionModule, definition.PermissionName, cancellationToken);
    }

    public async Task<AuthorizationSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var userId = LocalSessionStore.UserId ?? Guid.Empty;
        var companyId = LocalSessionStore.CompanyId ?? Guid.Empty;

        lock (_gate)
        {
            if (_cache != null && _cachedUserId == userId && _cachedCompanyId == companyId)
                return _cache;
        }

        var snapshot = await _access.GetSnapshotAsync(userId, companyId, cancellationToken);
        lock (_gate)
        {
            _cachedUserId = userId;
            _cachedCompanyId = companyId;
            _cache = snapshot;
        }

        return snapshot;
    }

    public Task<bool> UserBelongsToCurrentCompanyAsync(CancellationToken cancellationToken = default)
    {
        var userId = LocalSessionStore.UserId ?? Guid.Empty;
        var companyId = LocalSessionStore.CompanyId ?? Guid.Empty;
        return _access.UserBelongsToCompanyAsync(userId, companyId, cancellationToken);
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _cache = null;
            _cachedUserId = Guid.Empty;
            _cachedCompanyId = Guid.Empty;
        }
    }
}
