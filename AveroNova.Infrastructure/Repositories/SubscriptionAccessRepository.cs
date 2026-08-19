using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Repositories
{
    public sealed class SubscriptionAccessRepository : ISubscriptionAccessRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public SubscriptionAccessRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<Subscription?> GetCurrentForCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            return await db.Subscriptions
                .AsNoTracking()
                .Where(s => s.CompanyId == companyId && !s.IsDeleted)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<SubscriptionPlan?> GetPlanByIdAsync(Guid planId, CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            return await db.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == planId && !p.IsDeleted, cancellationToken);
        }

        public async Task<SubscriptionPlan?> GetPlanByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            return await db.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code == code && !p.IsDeleted, cancellationToken);
        }

        public async Task<IReadOnlyList<string>> GetEnabledModuleKeysAsync(Guid planId, CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            return await db.SubscriptionPlanFeatures
                .AsNoTracking()
                .Where(f => f.PlanId == planId && f.IsEnabled && !f.IsDeleted)
                .Select(f => f.ModuleKey)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<SubscriptionPlan>> GetCustomerAvailablePlansAsync(CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            return await db.SubscriptionPlans
                .AsNoTracking()
                .Where(p => p.IsCustomerAvailable && p.IsActive && !p.IsDeleted)
                .OrderBy(p => p.SortOrder)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> UserBelongsToCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var linked = await db.UserCompanies
                .AsNoTracking()
                .AnyAsync(uc => uc.UserId == userId
                                && uc.CompanyId == companyId
                                && uc.IsActive
                                && !uc.IsDeleted, cancellationToken);
            if (linked)
                return true;

            return await db.Companies
                .AsNoTracking()
                .AnyAsync(c => c.Id == companyId && c.UserId == userId && !c.IsDeleted, cancellationToken);
        }

        public async Task<IReadOnlyList<string>> GetUserPermissionNamesAsync(
            Guid userId,
            Guid companyId,
            CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var rows = await (
                from ur in db.UserRoles.AsNoTracking()
                join rp in db.RolePermissions.AsNoTracking() on ur.RoleId equals rp.RoleId
                join p in db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
                where ur.UserId == userId && !ur.IsDeleted && !rp.IsDeleted && !p.IsDeleted
                select new { ur.CompanyId, p.PermissionName }).ToListAsync(cancellationToken);

            var companyScoped = rows
                .Where(r => r.CompanyId == companyId)
                .Select(r => r.PermissionName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (companyScoped.Count > 0)
                return companyScoped;

            return rows
                .Where(r => r.CompanyId == null || r.CompanyId == Guid.Empty)
                .Select(r => r.PermissionName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task AddSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync(cancellationToken);
        }

        public async Task AddUserCompanyAsync(UserCompany userCompany, CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            db.UserCompanies.Add(userCompany);
            await db.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var existing = await db.Subscriptions.FirstOrDefaultAsync(s => s.Id == subscription.Id, cancellationToken);
            if (existing == null)
                return;

            existing.Status = subscription.Status;
            existing.IsActive = subscription.IsActive;
            existing.UpdatedAt = subscription.UpdatedAt;
            await db.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            return await db.Companies.AnyAsync(c => c.Id == companyId && !c.IsDeleted, cancellationToken);
        }

        public async Task<IReadOnlyList<Guid>> GetCompanyIdsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var membershipIds = await db.UserCompanies
                .AsNoTracking()
                .Where(uc => uc.UserId == userId && uc.IsActive && !uc.IsDeleted)
                .OrderByDescending(uc => uc.IsOwner)
                .ThenBy(uc => uc.CreatedAt)
                .Select(uc => uc.CompanyId)
                .ToListAsync(cancellationToken);

            var ownedIds = await db.Companies
                .AsNoTracking()
                .Where(c => c.UserId == userId && !c.IsDeleted)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            return membershipIds.Concat(ownedIds).Distinct().ToList();
        }
    }
}
