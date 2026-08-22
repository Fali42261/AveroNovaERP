using AveroNova.Application.DTOs;
using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Repositories
{
    public sealed class CompanyUserRepository : ICompanyUserRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CompanyUserRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<IReadOnlyList<CompanyUserListItem>> QueryAsync(
            Guid companyId,
            string? searchText,
            Guid? roleId,
            bool? isActive,
            CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty)
                return [];

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var query = MembershipQuery(db, companyId);

            if (isActive.HasValue)
                query = query.Where(uc => uc.IsActive == isActive.Value);

            var term = searchText?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(term))
            {
                query = query.Where(uc =>
                    uc.User.FullName.ToLower().Contains(term)
                    || uc.User.Email.ToLower().Contains(term)
                    || uc.User.MobileNumber.ToLower().Contains(term));
            }

            if (roleId.HasValue && roleId.Value != Guid.Empty)
            {
                query = query.Where(uc => db.UserRoles.Any(ur =>
                    ur.UserId == uc.UserId
                    && ur.CompanyId == companyId
                    && ur.RoleId == roleId.Value
                    && !ur.IsDeleted));
            }

            var memberships = await query
                .Include(uc => uc.User)
                .OrderByDescending(uc => uc.IsOwner)
                .ThenBy(uc => uc.User.FullName)
                .ToListAsync(cancellationToken);

            return await MapItemsAsync(db, companyId, memberships, cancellationToken);
        }

        public async Task<CompanyUserListItem?> GetByIdAsync(
            Guid companyId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty || userId == Guid.Empty)
                return null;

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var membership = await MembershipQuery(db, companyId)
                .Include(uc => uc.User)
                .FirstOrDefaultAsync(uc => uc.UserId == userId, cancellationToken);
            if (membership == null)
                return null;

            var items = await MapItemsAsync(db, companyId, [membership], cancellationToken);
            return items.Count == 0 ? null : items[0];
        }

        public async Task<bool> EmailExistsAsync(
            string email,
            Guid? excludeUserId,
            CancellationToken cancellationToken = default)
        {
            var normalized = email.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var query = db.Users.AsNoTracking()
                .Where(u => u.Email.ToLower() == normalized);
            if (excludeUserId is Guid exclude && exclude != Guid.Empty)
                query = query.Where(u => u.Id != exclude);
            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> IsOwnerAsync(
            Guid companyId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty || userId == Guid.Empty)
                return false;

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var viaMembership = await db.UserCompanies.AsNoTracking().AnyAsync(
                uc => uc.CompanyId == companyId
                      && uc.UserId == userId
                      && uc.IsOwner
                      && !uc.IsDeleted,
                cancellationToken);
            if (viaMembership)
                return true;

            return await db.Companies.AsNoTracking().AnyAsync(
                c => c.Id == companyId && c.UserId == userId && !c.IsDeleted,
                cancellationToken);
        }

        public async Task<bool> RoleIsAssignableAsync(
            Guid roleId,
            CancellationToken cancellationToken = default)
        {
            if (roleId == Guid.Empty)
                return false;

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var role = await db.Roles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == roleId && !r.IsDeleted, cancellationToken);
            return role != null
                   && RoleNames.IsAssignable(role.Name)
                   && !RoleNames.IsProtectedOwnerName(role.Name);
        }

        public async Task CreateInCompanyAsync(
            User user,
            UserCompany membership,
            UserRole assignment,
            CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                db.Users.Add(user);
                db.UserCompanies.Add(membership);
                db.UserRoles.Add(assignment);
                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task UpdateInCompanyAsync(
            Guid companyId,
            User user,
            Guid? roleId,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var existing = await db.Users.FirstOrDefaultAsync(
                    u => u.Id == user.Id && !u.IsDeleted, cancellationToken);
                var membership = await db.UserCompanies.FirstOrDefaultAsync(
                    uc => uc.UserId == user.Id && uc.CompanyId == companyId && !uc.IsDeleted,
                    cancellationToken);
                if (existing == null || membership == null)
                    throw new InvalidOperationException("User was not found in this company.");

                existing.FullName = user.FullName;
                existing.Email = user.Email;
                existing.MobileNumber = user.MobileNumber;
                existing.UpdatedAt = user.UpdatedAt;
                membership.IsActive = isActive;
                membership.UpdatedAt = user.UpdatedAt;

                var otherActive = await db.UserCompanies.AnyAsync(
                    uc => uc.UserId == user.Id
                          && uc.CompanyId != companyId
                          && uc.IsActive
                          && !uc.IsDeleted,
                    cancellationToken);
                existing.IsActiveUser = isActive || otherActive;

                if (roleId is Guid selectedRole && selectedRole != Guid.Empty)
                {
                    var current = await db.UserRoles
                        .Where(ur => ur.UserId == user.Id && ur.CompanyId == companyId && !ur.IsDeleted)
                        .ToListAsync(cancellationToken);
                    foreach (var row in current)
                    {
                        if (row.RoleId == selectedRole)
                            continue;
                        row.IsDeleted = true;
                        row.UpdatedAt = user.UpdatedAt;
                    }

                    if (!current.Any(ur => ur.RoleId == selectedRole))
                    {
                        db.UserRoles.Add(new UserRole
                        {
                            Id = Guid.NewGuid(),
                            UserId = user.Id,
                            RoleId = selectedRole,
                            CompanyId = companyId,
                            CreatedAt = DateTime.UtcNow,
                            IsDeleted = false
                        });
                    }
                }

                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<bool> SoftDeleteInCompanyAsync(
            Guid companyId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var membership = await db.UserCompanies.FirstOrDefaultAsync(
                    uc => uc.UserId == userId && uc.CompanyId == companyId && !uc.IsDeleted,
                    cancellationToken);
                if (membership == null)
                    return false;

                var now = DateTime.UtcNow;
                membership.IsDeleted = true;
                membership.IsActive = false;
                membership.UpdatedAt = now;

                var roles = await db.UserRoles
                    .Where(ur => ur.UserId == userId && ur.CompanyId == companyId && !ur.IsDeleted)
                    .ToListAsync(cancellationToken);
                foreach (var role in roles)
                {
                    role.IsDeleted = true;
                    role.UpdatedAt = now;
                }

                var remaining = await db.UserCompanies.AnyAsync(
                    uc => uc.UserId == userId && uc.CompanyId != companyId && !uc.IsDeleted,
                    cancellationToken);
                if (!remaining)
                {
                    var user = await db.Users.FirstOrDefaultAsync(
                        u => u.Id == userId && !u.IsDeleted, cancellationToken);
                    if (user != null)
                    {
                        user.IsDeleted = true;
                        user.IsActiveUser = false;
                        user.UpdatedAt = now;
                    }
                }

                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return true;
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<IReadOnlyList<Role>> GetAssignableRolesAsync(CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var roles = await db.Roles.AsNoTracking()
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.Name)
                .ToListAsync(cancellationToken);
            return roles
                .Where(r => RoleNames.IsAssignable(r.Name) && !RoleNames.IsProtectedOwnerName(r.Name))
                .ToList();
        }

        public async Task<IReadOnlyList<Role>> GetRolesUsedInCompanyAsync(
            Guid companyId,
            CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty)
                return [];

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var roleIds = await db.UserRoles.AsNoTracking()
                .Where(ur => ur.CompanyId == companyId && !ur.IsDeleted)
                .Select(ur => ur.RoleId)
                .Distinct()
                .ToListAsync(cancellationToken);

            return await db.Roles.AsNoTracking()
                .Where(r => !r.IsDeleted && roleIds.Contains(r.Id))
                .OrderBy(r => r.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<Role?> GetRoleByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
        {
            if (roleId == Guid.Empty)
                return null;

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            return await db.Roles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == roleId && !r.IsDeleted, cancellationToken);
        }

        public async Task<int> CountUsersWithRoleAsync(
            Guid companyId,
            Guid roleId,
            CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty || roleId == Guid.Empty)
                return 0;

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            return await db.UserRoles.AsNoTracking().CountAsync(
                ur => ur.CompanyId == companyId && ur.RoleId == roleId && !ur.IsDeleted,
                cancellationToken);
        }

        public async Task<bool> SoftDeleteRoleAsync(
            Guid roleId,
            CancellationToken cancellationToken = default)
        {
            if (roleId == Guid.Empty)
                return false;

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var role = await db.Roles.FirstOrDefaultAsync(
                r => r.Id == roleId && !r.IsDeleted, cancellationToken);
            if (role == null)
                return false;

            if (RoleNames.IsAssignable(role.Name) || RoleNames.IsProtectedOwnerName(role.Name))
                return false;

            var assigned = await db.UserRoles.AnyAsync(
                ur => ur.RoleId == roleId && !ur.IsDeleted, cancellationToken);
            if (assigned)
                return false;

            var now = DateTime.UtcNow;
            role.IsDeleted = true;
            role.UpdatedAt = now;

            var permissions = await db.RolePermissions
                .Where(rp => rp.RoleId == roleId && !rp.IsDeleted)
                .ToListAsync(cancellationToken);
            foreach (var permission in permissions)
            {
                permission.IsDeleted = true;
                permission.UpdatedAt = now;
            }

            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        private static IQueryable<UserCompany> MembershipQuery(AppDbContext db, Guid companyId)
            => db.UserCompanies
                .Where(uc => uc.CompanyId == companyId && !uc.IsDeleted && !uc.User.IsDeleted);

        private static async Task<IReadOnlyList<CompanyUserListItem>> MapItemsAsync(
            AppDbContext db,
            Guid companyId,
            IReadOnlyList<UserCompany> memberships,
            CancellationToken cancellationToken)
        {
            if (memberships.Count == 0)
                return [];

            var userIds = memberships.Select(m => m.UserId).ToList();
            var assignments = await (
                from ur in db.UserRoles.AsNoTracking()
                join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                where ur.CompanyId == companyId
                      && userIds.Contains(ur.UserId)
                      && !ur.IsDeleted
                      && !r.IsDeleted
                select new { ur.UserId, ur.RoleId, r.Name }).ToListAsync(cancellationToken);

            return memberships.Select(membership =>
            {
                var roles = assignments.Where(a => a.UserId == membership.UserId).ToList();
                return new CompanyUserListItem
                {
                    User = membership.User,
                    Membership = membership,
                    RoleNames = roles.Select(a => a.Name).ToList(),
                    RoleIds = roles.Select(a => a.RoleId).ToList()
                };
            }).ToList();
        }
    }
}
