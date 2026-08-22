using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Persistence
{
    /// <summary>
    /// Ensures the existing Customer entity is persisted in local SQLite.
    /// New databases get the table from the EF model; existing databases
    /// get CREATE TABLE IF NOT EXISTS without a full migration replay.
    /// </summary>
    public static class SqliteCustomerSchema
    {
        public static async Task EnsureAsync(AppDbContext db, CancellationToken cancellationToken = default)
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "Customers" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Customers" PRIMARY KEY,
                    "CompanyId" TEXT NOT NULL,
                    "Name" TEXT NOT NULL,
                    "MobileNumber" TEXT NOT NULL,
                    "Email" TEXT NOT NULL,
                    "Address" TEXT NOT NULL,
                    "City" TEXT NOT NULL,
                    "State" TEXT NOT NULL,
                    "Country" TEXT NOT NULL,
                    "PinCode" TEXT NOT NULL,
                    "TaxNumber" TEXT NOT NULL,
                    "Notes" TEXT NOT NULL,
                    "Status" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NULL,
                    "IsDeleted" INTEGER NOT NULL,
                    CONSTRAINT "FK_Customers_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT
                );
                """,
                cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_Customers_CompanyId"
                ON "Customers" ("CompanyId");
                """,
                cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_Customers_CompanyId_Name"
                ON "Customers" ("CompanyId", "Name");
                """,
                cancellationToken);
        }
    }
}
