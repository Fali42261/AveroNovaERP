using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Persistence;

public static class SqlitePaymentSchema
{
    public static async Task EnsureAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Payments" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Payments" PRIMARY KEY,
                "CompanyId" TEXT NOT NULL,
                "PaymentNumber" TEXT NOT NULL,
                "PartyId" TEXT NOT NULL,
                "PartyName" TEXT NOT NULL,
                "IsSupplier" INTEGER NOT NULL,
                "InvoiceId" TEXT NULL,
                "InvoiceNumber" TEXT NOT NULL,
                "Amount" TEXT NOT NULL,
                "PaymentDate" TEXT NOT NULL,
                "Method" INTEGER NOT NULL,
                "Reference" TEXT NOT NULL,
                "Notes" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "SyncStatus" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NULL,
                "IsDeleted" INTEGER NOT NULL
            );
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Payments_CompanyId\" ON \"Payments\" (\"CompanyId\");", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Payments_CompanyId_PaymentNumber\" ON \"Payments\" (\"CompanyId\", \"PaymentNumber\");", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Payments_CompanyId_PaymentDate\" ON \"Payments\" (\"CompanyId\", \"PaymentDate\");", cancellationToken);
    }
}
