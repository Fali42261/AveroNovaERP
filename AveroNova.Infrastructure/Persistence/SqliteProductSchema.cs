using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Persistence
{
    /// <summary>
    /// Ensures the existing Product entity is persisted in local SQLite.
    /// New databases get the table from the EF model; existing databases
    /// get CREATE TABLE IF NOT EXISTS without a full migration replay.
    /// </summary>
    public static class SqliteProductSchema
    {
        public static async Task EnsureAsync(AppDbContext db, CancellationToken cancellationToken = default)
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "Products" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY,
                    "CompanyId" TEXT NOT NULL,
                    "Name" TEXT NOT NULL,
                    "SKU" TEXT NOT NULL,
                    "Barcode" TEXT NOT NULL,
                    "Category" TEXT NOT NULL,
                    "Brand" TEXT NOT NULL,
                    "Unit" TEXT NOT NULL,
                    "PurchasePrice" TEXT NOT NULL,
                    "SellingPrice" TEXT NOT NULL,
                    "TaxPercent" TEXT NOT NULL,
                    "DiscountPercent" TEXT NOT NULL,
                    "Stock" INTEGER NOT NULL,
                    "OpeningStock" INTEGER NOT NULL,
                    "MinimumStock" INTEGER NOT NULL,
                    "Description" TEXT NOT NULL,
                    "Status" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NULL,
                    "IsDeleted" INTEGER NOT NULL,
                    CONSTRAINT "FK_Products_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT
                );
                """,
                cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_Products_CompanyId"
                ON "Products" ("CompanyId");
                """,
                cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_Products_CompanyId_Name"
                ON "Products" ("CompanyId", "Name");
                """,
                cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_Products_CompanyId_SKU"
                ON "Products" ("CompanyId", "SKU");
                """,
                cancellationToken);
        }
    }
}
