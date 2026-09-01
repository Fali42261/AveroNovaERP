using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Persistence;

public static class SqlitePurchaseSchema
{
    public static async Task EnsureAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Purchases" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Purchases" PRIMARY KEY,
                "CompanyId" TEXT NOT NULL,
                "PurchaseNumber" TEXT NOT NULL,
                "SupplierId" TEXT NOT NULL,
                "SupplierName" TEXT NOT NULL,
                "PurchaseDate" TEXT NOT NULL,
                "DueDate" TEXT NOT NULL,
                "PaymentMethod" INTEGER NOT NULL,
                "PaidAmount" TEXT NOT NULL,
                "Reference" TEXT NOT NULL,
                "Notes" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "SyncStatus" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NULL,
                "IsDeleted" INTEGER NOT NULL
            );
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PurchaseItems" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_PurchaseItems" PRIMARY KEY,
                "PurchaseId" TEXT NOT NULL,
                "ProductId" TEXT NOT NULL,
                "ProductName" TEXT NOT NULL,
                "SKU" TEXT NOT NULL,
                "UnitPrice" TEXT NOT NULL,
                "Quantity" INTEGER NOT NULL,
                "TaxPct" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NULL,
                "IsDeleted" INTEGER NOT NULL,
                CONSTRAINT "FK_PurchaseItems_Purchases_PurchaseId" FOREIGN KEY ("PurchaseId") REFERENCES "Purchases" ("Id") ON DELETE CASCADE
            );
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Purchases_CompanyId\" ON \"Purchases\" (\"CompanyId\");", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Purchases_CompanyId_PurchaseNumber\" ON \"Purchases\" (\"CompanyId\", \"PurchaseNumber\");", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_PurchaseItems_PurchaseId\" ON \"PurchaseItems\" (\"PurchaseId\");", cancellationToken);
    }
}
