using System.Data;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Persistence
{
    public static class SqliteSubscriptionSchema
    {
        public static async Task EnsureAsync(AppDbContext db, CancellationToken cancellationToken = default)
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "SubscriptionPlans" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_SubscriptionPlans" PRIMARY KEY,
                    "Code" TEXT NOT NULL,
                    "Name" TEXT NOT NULL,
                    "Description" TEXT NOT NULL,
                    "DurationInDays" INTEGER NOT NULL,
                    "IsTrialPlan" INTEGER NOT NULL,
                    "IsCustomerAvailable" INTEGER NOT NULL,
                    "IsActive" INTEGER NOT NULL,
                    "SortOrder" INTEGER NOT NULL,
                    "Price" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NULL,
                    "IsDeleted" INTEGER NOT NULL
                );
                """, cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SubscriptionPlans_Code"
                ON "SubscriptionPlans" ("Code");
                """, cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "SubscriptionPlanFeatures" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_SubscriptionPlanFeatures" PRIMARY KEY,
                    "PlanId" TEXT NOT NULL,
                    "ModuleKey" TEXT NOT NULL,
                    "ModuleName" TEXT NOT NULL,
                    "IsEnabled" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NULL,
                    "IsDeleted" INTEGER NOT NULL,
                    CONSTRAINT "FK_SubscriptionPlanFeatures_SubscriptionPlans_PlanId"
                        FOREIGN KEY ("PlanId") REFERENCES "SubscriptionPlans" ("Id") ON DELETE CASCADE
                );
                """, cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SubscriptionPlanFeatures_PlanId_ModuleKey"
                ON "SubscriptionPlanFeatures" ("PlanId", "ModuleKey");
                """, cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "UserCompanies" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_UserCompanies" PRIMARY KEY,
                    "UserId" TEXT NOT NULL,
                    "CompanyId" TEXT NOT NULL,
                    "IsOwner" INTEGER NOT NULL,
                    "IsActive" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NULL,
                    "IsDeleted" INTEGER NOT NULL,
                    CONSTRAINT "FK_UserCompanies_Users_UserId"
                        FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_UserCompanies_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT
                );
                """, cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserCompanies_UserId_CompanyId"
                ON "UserCompanies" ("UserId", "CompanyId");
                """, cancellationToken);

            await EnsureSubscriptionColumnAsync(db, "PlanId", cancellationToken);
            await EnsureSubscriptionColumnAsync(db, "PlanName", cancellationToken);
            await EnsureSubscriptionColumnAsync(db, "Price", cancellationToken);
            await EnsureSubscriptionColumnAsync(db, "DurationInDays", cancellationToken);
            await EnsureSubscriptionColumnAsync(db, "StartDate", cancellationToken);
            await EnsureSubscriptionColumnAsync(db, "ExpiryDate", cancellationToken);
            await EnsureSubscriptionColumnAsync(db, "SubscriptionType", cancellationToken);
            await EnsureSubscriptionColumnAsync(db, "Plan", cancellationToken);
            await EnsureSubscriptionColumnAsync(db, "Status", cancellationToken);
            await EnsureSubscriptionColumnAsync(db, "TrialStartDate", cancellationToken);
            await EnsureSubscriptionColumnAsync(db, "TrialEndDate", cancellationToken);
            await EnsureSubscriptionColumnAsync(db, "IsTrial", cancellationToken);
            await EnsureSubscriptionColumnAsync(db, "IsActive", cancellationToken);
            await EnsureSubscriptionColumnAsync(db, "AutoRenew", cancellationToken);
            await EnsureSubscriptionColumnAsync(db, "IsSubscription", cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_Subscriptions_PlanId"
                ON "Subscriptions" ("PlanId");
                """, cancellationToken);
        }

        private static async Task EnsureSubscriptionColumnAsync(
            AppDbContext db,
            string column,
            CancellationToken cancellationToken)
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
                await connection.OpenAsync(cancellationToken);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Subscriptions') WHERE name = $name";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "$name";
                parameter.Value = column;
                command.Parameters.Add(parameter);

                var result = await command.ExecuteScalarAsync(cancellationToken);
                var exists = Convert.ToInt32(result ?? 0) > 0;
                if (exists)
                    return;
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }

            var sql = column switch
            {
                "PlanId" => """ALTER TABLE "Subscriptions" ADD COLUMN "PlanId" TEXT NULL;""",
                "PlanName" => """ALTER TABLE "Subscriptions" ADD COLUMN "PlanName" TEXT NOT NULL DEFAULT '';""",
                "Price" => """ALTER TABLE "Subscriptions" ADD COLUMN "Price" TEXT NOT NULL DEFAULT 0;""",
                "DurationInDays" => """ALTER TABLE "Subscriptions" ADD COLUMN "DurationInDays" INTEGER NOT NULL DEFAULT 0;""",
                "StartDate" => """ALTER TABLE "Subscriptions" ADD COLUMN "StartDate" TEXT NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';""",
                "ExpiryDate" => """ALTER TABLE "Subscriptions" ADD COLUMN "ExpiryDate" TEXT NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';""",
                "SubscriptionType" => """ALTER TABLE "Subscriptions" ADD COLUMN "SubscriptionType" INTEGER NOT NULL DEFAULT 1;""",
                "Plan" => """ALTER TABLE "Subscriptions" ADD COLUMN "Plan" INTEGER NOT NULL DEFAULT 0;""",
                "Status" => """ALTER TABLE "Subscriptions" ADD COLUMN "Status" INTEGER NOT NULL DEFAULT 0;""",
                "TrialStartDate" => """ALTER TABLE "Subscriptions" ADD COLUMN "TrialStartDate" TEXT NULL;""",
                "TrialEndDate" => """ALTER TABLE "Subscriptions" ADD COLUMN "TrialEndDate" TEXT NULL;""",
                "IsTrial" => """ALTER TABLE "Subscriptions" ADD COLUMN "IsTrial" INTEGER NOT NULL DEFAULT 0;""",
                "IsActive" => """ALTER TABLE "Subscriptions" ADD COLUMN "IsActive" INTEGER NOT NULL DEFAULT 0;""",
                "AutoRenew" => """ALTER TABLE "Subscriptions" ADD COLUMN "AutoRenew" INTEGER NOT NULL DEFAULT 0;""",
                "IsSubscription" => """ALTER TABLE "Subscriptions" ADD COLUMN "IsSubscription" INTEGER NOT NULL DEFAULT 0;""",
                _ => throw new ArgumentOutOfRangeException(nameof(column), column, "Unknown subscription column.")
            };

            await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }
}
