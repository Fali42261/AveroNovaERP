using System.Text.Json;
using AveroNova.Application.DTOs.Auth;
using AveroNova.App.UI.Data;
using AveroNova.Domain.Constants;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public interface IOfflineRegistrationStore
{
    Task<(Guid UserId, Guid CompanyId, Guid UserCompanyId, Guid SubscriptionId)> SaveOfflineRegistrationAsync(
        RegisterRequest request,
        Guid installationId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists offline registration identity into local SQLite and enqueues Pending sync items.
/// Password is never written here.
/// </summary>
public sealed class OfflineRegistrationStore : IOfflineRegistrationStore
{
    public static readonly string[] OwnerPermissions =
    [
        "Dashboard.View", "Sales.View", "Sales.Create", "Inventory.View",
        "Customers.View", "Reports.View", "Company.Manage", "Users.Manage"
    ];

    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;

    public OfflineRegistrationStore(IDbContextFactory<LocalAppDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<(Guid UserId, Guid CompanyId, Guid UserCompanyId, Guid SubscriptionId)> SaveOfflineRegistrationAsync(
        RegisterRequest request,
        Guid installationId,
        CancellationToken cancellationToken = default)
    {
        var userId = request.ClientUserId is Guid u && u != Guid.Empty ? u : Guid.NewGuid();
        var companyId = request.ClientCompanyId is Guid c && c != Guid.Empty ? c : Guid.NewGuid();
        var userCompanyId = request.ClientUserCompanyId is Guid uc && uc != Guid.Empty ? uc : Guid.NewGuid();
        var subscriptionId = request.ClientSubscriptionId is Guid s && s != Guid.Empty ? s : Guid.NewGuid();
        var now = DateTime.UtcNow;
        var trialEnd = now.AddDays(15);

        var meta = new OfflineRegistrationPayload
        {
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            MobileNumber = request.MobileNumber.Trim(),
            CompanyName = request.CompanyName.Trim(),
            CompanyEmail = request.CompanyEmail.Trim().ToLowerInvariant(),
            CompanyMobile = request.CompanyMobile.Trim(),
            OwnerName = string.IsNullOrWhiteSpace(request.OwnerName) ? request.FullName.Trim() : request.OwnerName.Trim(),
            Plan = "Starter",
            InstallationId = installationId,
            DeviceId = request.DeviceId,
            DeviceName = request.DeviceName,
            Platform = request.Platform,
            ClientUserId = userId,
            ClientCompanyId = companyId,
            ClientUserCompanyId = userCompanyId,
            ClientSubscriptionId = subscriptionId,
            TrialStartDateUtc = now,
            TrialEndDateUtc = trialEnd
        };
        var payloadJson = JsonSerializer.Serialize(meta);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        db.Users.Add(new LocalUserEntity
        {
            Id = userId,
            FullName = meta.FullName,
            Email = meta.Email,
            MobileNumber = meta.MobileNumber,
            IsActive = true
        });
        db.Companies.Add(new LocalCompanyEntity
        {
            Id = companyId,
            CompanyName = meta.CompanyName,
            Email = meta.CompanyEmail,
            MobileNumber = meta.CompanyMobile,
            IsActive = true
        });
        db.UserCompanies.Add(new LocalUserCompanyEntity
        {
            Id = userCompanyId,
            UserId = userId,
            CompanyId = companyId,
            IsDefault = true,
            IsOwner = true,
            IsActive = true
        });
        db.Subscriptions.Add(new LocalSubscriptionEntity
        {
            Id = subscriptionId,
            CompanyId = companyId,
            PlanName = "Starter",
            IsTrial = true,
            StartDateUtc = now,
            EndDateUtc = trialEnd,
            IsActive = true
        });
        db.Roles.Add(new LocalRoleEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CompanyId = companyId,
            RoleName = RoleNames.CompanyOwner
        });
        foreach (var permission in OwnerPermissions)
        {
            db.Permissions.Add(new LocalPermissionEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CompanyId = companyId,
                PermissionName = permission
            });
        }

        Enqueue(db, "User", userId, companyId, payloadJson, now);
        Enqueue(db, "Company", companyId, companyId, payloadJson, now);
        Enqueue(db, "UserCompany", userCompanyId, companyId, payloadJson, now);
        Enqueue(db, "Subscription", subscriptionId, companyId, payloadJson, now);

        await db.SaveChangesAsync(cancellationToken);
        return (userId, companyId, userCompanyId, subscriptionId);
    }

    private static void Enqueue(
        LocalAppDbContext db,
        string entityType,
        Guid entityId,
        Guid companyId,
        string payloadJson,
        DateTime now)
    {
        db.SyncQueue.Add(new LocalSyncQueueEntity
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Operation = (int)SyncOperation.Create,
            Status = (int)RecordSyncStatus.Pending,
            RetryCount = 0,
            CreatedAt = now,
            CompanyId = companyId,
            PayloadJson = payloadJson
        });
    }
}

public sealed class OfflineRegistrationPayload
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyEmail { get; set; } = string.Empty;
    public string CompanyMobile { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string Plan { get; set; } = "Starter";
    public Guid InstallationId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public Guid ClientUserId { get; set; }
    public Guid ClientCompanyId { get; set; }
    public Guid ClientUserCompanyId { get; set; }
    public Guid ClientSubscriptionId { get; set; }
    public DateTime TrialStartDateUtc { get; set; }
    public DateTime TrialEndDateUtc { get; set; }
}
