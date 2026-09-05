using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AveroNova.App.UI.Data;

public interface ILocalDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    string DatabasePath { get; }
}

public sealed class LocalDatabaseInitializer : ILocalDatabaseInitializer
{
    public const int CurrentSchemaVersion = 9;

    private readonly LocalAppDbContext _db;
    private readonly ILogger<LocalDatabaseInitializer> _logger;

    public LocalDatabaseInitializer(LocalAppDbContext db, ILogger<LocalDatabaseInitializer> logger)
    {
        _db = db;
        _logger = logger;
        DatabasePath = _db.Database.GetDbConnection().DataSource;
    }

    public string DatabasePath { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureInstallationsTableAsync(cancellationToken);
        await EnsureSessionColumnsAsync(cancellationToken);
        await EnsureSyncAndSubscriptionTablesAsync(cancellationToken);
        await EnsureLicenseTableAsync(cancellationToken);
        await EnsureBusinessTablesAsync(cancellationToken);

        var info = await _db.SchemaInfo.FirstOrDefaultAsync(cancellationToken);
        if (info is null)
        {
            _db.SchemaInfo.Add(new LocalSchemaInfoEntity
            {
                Id = 1,
                SchemaVersion = CurrentSchemaVersion,
                AppliedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Local SQLite initialized at {Path} (schema v{Version}).", DatabasePath, CurrentSchemaVersion);
            return;
        }

        if (info.SchemaVersion < CurrentSchemaVersion)
        {
            info.SchemaVersion = CurrentSchemaVersion;
            info.AppliedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Local SQLite upgraded to schema v{Version}.", CurrentSchemaVersion);
        }
    }

    private async Task EnsureInstallationsTableAsync(CancellationToken cancellationToken)
    {
        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "LocalInstallations" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_LocalInstallations" PRIMARY KEY,
                "InstallationId" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "RegisteredAtUtc" TEXT NULL,
                "DeviceId" TEXT NOT NULL,
                "UserId" TEXT NULL,
                "CompanyId" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL
            );
            """,
            cancellationToken);

        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_LocalInstallations_InstallationId"
            ON "LocalInstallations" ("InstallationId");
            """,
            cancellationToken);
    }

    private async Task EnsureSessionColumnsAsync(CancellationToken cancellationToken)
    {
        await TryAddColumnAsync("LocalSessions", "InstallationId", "TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'", cancellationToken);
        await TryAddColumnAsync("LocalSessions", "ServerSessionId", "TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'", cancellationToken);
        await TryAddColumnAsync("LocalSessions", "LastAuthenticatedAtUtc", "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'", cancellationToken);
    }

    private async Task EnsureSyncAndSubscriptionTablesAsync(CancellationToken cancellationToken)
    {
        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "LocalSubscriptions" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_LocalSubscriptions" PRIMARY KEY,
                "CompanyId" TEXT NOT NULL,
                "PlanName" TEXT NOT NULL,
                "IsTrial" INTEGER NOT NULL,
                "StartDateUtc" TEXT NOT NULL,
                "EndDateUtc" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL
            );
            """,
            cancellationToken);

        await TryAddColumnAsync("LocalSyncQueue", "SyncedAt", "TEXT NULL", cancellationToken);
        await TryAddColumnAsync("LocalSyncQueue", "PayloadJson", "TEXT NULL", cancellationToken);
    }

    private async Task EnsureLicenseTableAsync(CancellationToken cancellationToken)
    {
        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "LocalLicenses" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_LocalLicenses" PRIMARY KEY,
                "LicenseId" TEXT NOT NULL,
                "UserId" TEXT NULL,
                "CompanyId" TEXT NULL,
                "DeviceId" TEXT NOT NULL,
                "Plan" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "IsTrial" INTEGER NOT NULL,
                "TrialStartDateUtc" TEXT NOT NULL,
                "TrialEndDateUtc" TEXT NOT NULL,
                "ExpiryDateUtc" TEXT NULL,
                "LastValidatedAtUtc" TEXT NULL,
                "LastSyncedAtUtc" TEXT NULL,
                "LastKnownServerTimeUtc" TEXT NULL,
                "LastKnownTrustedTimeUtc" TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                "IsServerAuthoritative" INTEGER NOT NULL DEFAULT 0,
                "ClockRollbackDetected" INTEGER NOT NULL DEFAULT 0,
                "UpdatedAtUtc" TEXT NOT NULL
            );
            """,
            cancellationToken);

        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_LocalLicenses_LicenseId"
            ON "LocalLicenses" ("LicenseId");
            """,
            cancellationToken);

        await TryAddColumnAsync("LocalLicenses", "LastKnownTrustedTimeUtc", "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'", cancellationToken);
        await TryAddColumnAsync("LocalLicenses", "IsServerAuthoritative", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await TryAddColumnAsync("LocalLicenses", "ClockRollbackDetected", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
    }

    private async Task EnsureBusinessTablesAsync(CancellationToken cancellationToken)
    {
        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "LocalCustomers" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_LocalCustomers" PRIMARY KEY,
                "ServerId" TEXT NULL,
                "CompanyId" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Email" TEXT NOT NULL,
                "Phone" TEXT NOT NULL,
                "Address" TEXT NOT NULL,
                "City" TEXT NOT NULL,
                "Country" TEXT NOT NULL,
                "TaxNumber" TEXT NOT NULL,
                "Notes" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "OutstandingBalance" TEXT NOT NULL,
                "TotalPurchases" TEXT NOT NULL,
                "SyncStatus" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                "LastSyncedAtUtc" TEXT NULL,
                "SyncError" TEXT NULL
            );
            """,
            cancellationToken);

        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "LocalProducts" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_LocalProducts" PRIMARY KEY,
                "ServerId" TEXT NULL,
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
                "Stock" INTEGER NOT NULL,
                "MinimumStock" INTEGER NOT NULL,
                "Description" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "SyncStatus" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                "LastSyncedAtUtc" TEXT NULL,
                "SyncError" TEXT NULL
            );
            """,
            cancellationToken);

        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "LocalInvoices" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_LocalInvoices" PRIMARY KEY,
                "ServerId" TEXT NULL,
                "CompanyId" TEXT NOT NULL,
                "InvoiceNumber" TEXT NOT NULL,
                "CustomerId" TEXT NOT NULL,
                "CustomerName" TEXT NOT NULL,
                "InvoiceDate" TEXT NOT NULL,
                "DueDate" TEXT NOT NULL,
                "ItemsJson" TEXT NOT NULL,
                "DiscountPct" TEXT NOT NULL,
                "TaxPct" TEXT NOT NULL,
                "PaymentMethod" INTEGER NOT NULL,
                "Notes" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "PaidAmount" TEXT NOT NULL,
                "SyncStatus" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                "LastSyncedAtUtc" TEXT NULL,
                "SyncError" TEXT NULL
            );
            """,
            cancellationToken);

        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "LocalPayments" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_LocalPayments" PRIMARY KEY,
                "ServerId" TEXT NULL,
                "CompanyId" TEXT NOT NULL,
                "PaymentNumber" TEXT NOT NULL,
                "PartyId" TEXT NOT NULL,
                "PartyName" TEXT NOT NULL,
                "IsSupplier" INTEGER NOT NULL,
                "InvoiceId" TEXT NULL,
                "InvoiceNumber" TEXT NOT NULL,
                "Amount" TEXT NOT NULL,
                "Method" INTEGER NOT NULL,
                "PaymentDate" TEXT NOT NULL,
                "Reference" TEXT NOT NULL,
                "Notes" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "SyncStatus" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                "LastSyncedAtUtc" TEXT NULL,
                "SyncError" TEXT NULL
            );
            """,
            cancellationToken);

        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "LocalStockMovements" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_LocalStockMovements" PRIMARY KEY,
                "ServerId" TEXT NULL,
                "CompanyId" TEXT NOT NULL,
                "ProductId" TEXT NOT NULL,
                "ProductName" TEXT NOT NULL,
                "SKU" TEXT NOT NULL,
                "Type" INTEGER NOT NULL,
                "Quantity" INTEGER NOT NULL,
                "StockBefore" INTEGER NOT NULL,
                "StockAfter" INTEGER NOT NULL,
                "Reference" TEXT NOT NULL,
                "Notes" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "SyncStatus" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                "LastSyncedAtUtc" TEXT NULL,
                "SyncError" TEXT NULL
            );
            """,
            cancellationToken);

        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "LocalSuppliers" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_LocalSuppliers" PRIMARY KEY,
                "ServerId" TEXT NULL, "CompanyId" TEXT NOT NULL,
                "Name" TEXT NOT NULL, "Email" TEXT NOT NULL, "Phone" TEXT NOT NULL,
                "Address" TEXT NOT NULL, "TaxNumber" TEXT NOT NULL, "Notes" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL, "SyncStatus" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL, "UpdatedAtUtc" TEXT NOT NULL,
                "LastSyncedAtUtc" TEXT NULL, "SyncError" TEXT NULL
            );
            """, cancellationToken);

        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "LocalPurchases" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_LocalPurchases" PRIMARY KEY,
                "ServerId" TEXT NULL, "CompanyId" TEXT NOT NULL,
                "PurchaseNumber" TEXT NOT NULL, "SupplierId" TEXT NOT NULL,
                "SupplierName" TEXT NOT NULL, "PurchaseDate" TEXT NOT NULL, "DueDate" TEXT NOT NULL,
                "ItemsJson" TEXT NOT NULL, "PaymentMethod" INTEGER NOT NULL,
                "Reference" TEXT NOT NULL, "Notes" TEXT NOT NULL, "Status" INTEGER NOT NULL,
                "PaidAmount" TEXT NOT NULL, "SyncStatus" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL, "UpdatedAtUtc" TEXT NOT NULL,
                "LastSyncedAtUtc" TEXT NULL, "SyncError" TEXT NULL
            );
            """, cancellationToken);

        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "LocalExpenses" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_LocalExpenses" PRIMARY KEY,
                "ServerId" TEXT NULL, "CompanyId" TEXT NOT NULL,
                "Category" TEXT NOT NULL, "Description" TEXT NOT NULL,
                "Amount" TEXT NOT NULL, "ExpenseDate" TEXT NOT NULL, "Method" INTEGER NOT NULL,
                "Reference" TEXT NOT NULL, "Notes" TEXT NOT NULL, "Status" INTEGER NOT NULL,
                "ApprovedBy" TEXT NOT NULL, "SyncStatus" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL, "UpdatedAtUtc" TEXT NOT NULL,
                "LastSyncedAtUtc" TEXT NULL, "SyncError" TEXT NULL
            );
            """, cancellationToken);

        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_LocalCustomers_CompanyId" ON "LocalCustomers" ("CompanyId");
            """,
            cancellationToken);
        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_LocalProducts_CompanyId" ON "LocalProducts" ("CompanyId");
            """,
            cancellationToken);
        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_LocalInvoices_CompanyId" ON "LocalInvoices" ("CompanyId");
            """,
            cancellationToken);
        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_LocalPayments_CompanyId" ON "LocalPayments" ("CompanyId");
            """,
            cancellationToken);
        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_LocalStockMovements_CompanyId_CreatedAtUtc"
            ON "LocalStockMovements" ("CompanyId", "CreatedAtUtc");
            """,
            cancellationToken);
        await _db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_LocalStockMovements_CompanyId_ProductId"
            ON "LocalStockMovements" ("CompanyId", "ProductId");
            """,
            cancellationToken);
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_LocalSuppliers_CompanyId_Name\" ON \"LocalSuppliers\" (\"CompanyId\", \"Name\");",
            cancellationToken);
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_LocalPurchases_CompanyId_PurchaseNumber\" ON \"LocalPurchases\" (\"CompanyId\", \"PurchaseNumber\");",
            cancellationToken);
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_LocalPurchases_CompanyId_SupplierId\" ON \"LocalPurchases\" (\"CompanyId\", \"SupplierId\");",
            cancellationToken);
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_LocalExpenses_CompanyId_ExpenseDate\" ON \"LocalExpenses\" (\"CompanyId\", \"ExpenseDate\");",
            cancellationToken);
    }

    private async Task TryAddColumnAsync(string table, string column, string definition, CancellationToken cancellationToken)
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition};",
                cancellationToken);
        }
        catch
        {
            // Column already exists
        }
    }
}
