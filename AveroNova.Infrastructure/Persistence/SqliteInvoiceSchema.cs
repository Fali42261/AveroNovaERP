using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Persistence;

public static class SqliteInvoiceSchema
{
    public static async Task EnsureAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Invoices" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Invoices" PRIMARY KEY,
                "CompanyId" TEXT NOT NULL,
                "InvoiceNumber" TEXT NOT NULL,
                "CustomerId" TEXT NOT NULL,
                "CustomerName" TEXT NOT NULL,
                "InvoiceDate" TEXT NOT NULL,
                "DueDate" TEXT NOT NULL,
                "DiscountPct" TEXT NOT NULL,
                "TaxPct" TEXT NOT NULL,
                "PaymentMethod" INTEGER NOT NULL,
                "PaidAmount" TEXT NOT NULL,
                "Notes" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "SyncStatus" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NULL,
                "IsDeleted" INTEGER NOT NULL,
                CONSTRAINT "FK_Invoices_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT
            );
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "InvoiceItems" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_InvoiceItems" PRIMARY KEY,
                "InvoiceId" TEXT NOT NULL,
                "ProductId" TEXT NOT NULL,
                "ProductName" TEXT NOT NULL,
                "SKU" TEXT NOT NULL,
                "UnitPrice" TEXT NOT NULL,
                "Quantity" INTEGER NOT NULL,
                "DiscountPct" TEXT NOT NULL,
                "TaxPct" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NULL,
                "IsDeleted" INTEGER NOT NULL,
                CONSTRAINT "FK_InvoiceItems_Invoices_InvoiceId" FOREIGN KEY ("InvoiceId") REFERENCES "Invoices" ("Id") ON DELETE CASCADE
            );
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "StockMovements" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_StockMovements" PRIMARY KEY,
                "CompanyId" TEXT NOT NULL,
                "ProductId" TEXT NOT NULL,
                "MovementType" INTEGER NOT NULL,
                "Quantity" INTEGER NOT NULL,
                "StockBefore" INTEGER NOT NULL,
                "StockAfter" INTEGER NOT NULL,
                "Reference" TEXT NOT NULL,
                "Notes" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "SyncStatus" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NULL,
                "IsDeleted" INTEGER NOT NULL,
                CONSTRAINT "FK_StockMovements_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_StockMovements_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
            );
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Invoices_CompanyId\" ON \"Invoices\" (\"CompanyId\");", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Invoices_CompanyId_InvoiceNumber\" ON \"Invoices\" (\"CompanyId\", \"InvoiceNumber\");", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_InvoiceItems_InvoiceId\" ON \"InvoiceItems\" (\"InvoiceId\");", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_StockMovements_Company_Product_Created\" ON \"StockMovements\" (\"CompanyId\", \"ProductId\", \"CreatedAt\");", cancellationToken);
    }
}
