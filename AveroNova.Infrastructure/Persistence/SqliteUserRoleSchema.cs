using System.Data;
using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Persistence
{
    public static class SqliteUserRoleSchema
    {
        public static async Task EnsureAsync(AppDbContext db, CancellationToken cancellationToken = default)
        {
            await EnsureCompanyIdColumnAsync(db, cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """DROP INDEX IF EXISTS "IX_UserRoles_UserId_RoleId";""",
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserRoles_UserId_CompanyId_RoleId"
                ON "UserRoles" ("UserId", "CompanyId", "RoleId");
                """,
                cancellationToken);
            await BackfillCompanyIdsAsync(db, cancellationToken);
        }

        private static async Task EnsureCompanyIdColumnAsync(AppDbContext db, CancellationToken cancellationToken)
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
                await connection.OpenAsync(cancellationToken);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('UserRoles') WHERE name = 'CompanyId'";
                var result = await command.ExecuteScalarAsync(cancellationToken);
                if (Convert.ToInt32(result ?? 0) > 0)
                    return;
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }

            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "UserRoles" ADD COLUMN "CompanyId" TEXT NULL;""",
                cancellationToken);
        }

        private static async Task BackfillCompanyIdsAsync(AppDbContext db, CancellationToken cancellationToken)
        {
            var unscoped = await db.UserRoles
                .Where(ur => !ur.IsDeleted && (ur.CompanyId == null || ur.CompanyId == Guid.Empty))
                .ToListAsync(cancellationToken);
            if (unscoped.Count == 0)
                return;

            var now = DateTime.UtcNow;
            foreach (var assignment in unscoped)
            {
                var companyIds = await db.UserCompanies
                    .AsNoTracking()
                    .Where(uc => uc.UserId == assignment.UserId && uc.IsActive && !uc.IsDeleted)
                    .OrderByDescending(uc => uc.IsOwner)
                    .ThenBy(uc => uc.CreatedAt)
                    .Select(uc => uc.CompanyId)
                    .ToListAsync(cancellationToken);

                if (companyIds.Count == 0)
                {
                    companyIds = await db.Companies
                        .AsNoTracking()
                        .Where(c => c.UserId == assignment.UserId && !c.IsDeleted)
                        .Select(c => c.Id)
                        .ToListAsync(cancellationToken);
                }

                if (companyIds.Count == 0)
                    continue;

                assignment.CompanyId = companyIds[0];
                assignment.UpdatedAt = now;

                for (var i = 1; i < companyIds.Count; i++)
                {
                    var exists = await db.UserRoles.AnyAsync(
                        ur => ur.UserId == assignment.UserId
                              && ur.RoleId == assignment.RoleId
                              && ur.CompanyId == companyIds[i]
                              && !ur.IsDeleted,
                        cancellationToken);
                    if (exists)
                        continue;

                    db.UserRoles.Add(new UserRole
                    {
                        Id = Guid.NewGuid(),
                        UserId = assignment.UserId,
                        RoleId = assignment.RoleId,
                        CompanyId = companyIds[i],
                        CreatedAt = now,
                        IsDeleted = false
                    });
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
