using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Data;

/// <summary>
/// Local SQLite database for Offline-First auth session context + sync queue.
/// Sensitive tokens are NOT stored here — use <see cref="Services.Security.ISecureTokenStore"/>.
/// </summary>
public sealed class LocalAppDbContext : DbContext
{
    public LocalAppDbContext(DbContextOptions<LocalAppDbContext> options) : base(options)
    {
    }

    public DbSet<LocalInstallationEntity> Installations => Set<LocalInstallationEntity>();
    public DbSet<LocalSessionEntity> Sessions => Set<LocalSessionEntity>();
    public DbSet<LocalUserEntity> Users => Set<LocalUserEntity>();
    public DbSet<LocalCompanyEntity> Companies => Set<LocalCompanyEntity>();
    public DbSet<LocalUserCompanyEntity> UserCompanies => Set<LocalUserCompanyEntity>();
    public DbSet<LocalCompanyRoleEntity> CompanyRoles => Set<LocalCompanyRoleEntity>();
    public DbSet<LocalCompanyRolePermissionEntity> CompanyRolePermissions => Set<LocalCompanyRolePermissionEntity>();
    public DbSet<LocalRoleEntity> Roles => Set<LocalRoleEntity>();
    public DbSet<LocalPermissionEntity> Permissions => Set<LocalPermissionEntity>();
    public DbSet<LocalSubscriptionEntity> Subscriptions => Set<LocalSubscriptionEntity>();
    public DbSet<LocalLicenseEntity> Licenses => Set<LocalLicenseEntity>();
    public DbSet<LocalCustomerEntity> Customers => Set<LocalCustomerEntity>();
    public DbSet<LocalProductEntity> Products => Set<LocalProductEntity>();
    public DbSet<LocalStockMovementEntity> StockMovements => Set<LocalStockMovementEntity>();
    public DbSet<LocalSupplierEntity> Suppliers => Set<LocalSupplierEntity>();
    public DbSet<LocalPurchaseEntity> Purchases => Set<LocalPurchaseEntity>();
    public DbSet<LocalExpenseEntity> Expenses => Set<LocalExpenseEntity>();
    public DbSet<LocalSalesReturnEntity> SalesReturns => Set<LocalSalesReturnEntity>();
    public DbSet<LocalPurchaseReturnEntity> PurchaseReturns => Set<LocalPurchaseReturnEntity>();
    public DbSet<LocalInvoiceEntity> Invoices => Set<LocalInvoiceEntity>();
    public DbSet<LocalPaymentEntity> Payments => Set<LocalPaymentEntity>();
    public DbSet<LocalSyncQueueEntity> SyncQueue => Set<LocalSyncQueueEntity>();
    public DbSet<LocalSchemaInfoEntity> SchemaInfo => Set<LocalSchemaInfoEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocalInstallationEntity>(e =>
        {
            e.ToTable("LocalInstallations");
            e.HasKey(x => x.Id);
            e.Property(x => x.DeviceId).HasMaxLength(128);
            e.HasIndex(x => x.InstallationId).IsUnique();
        });

        modelBuilder.Entity<LocalSessionEntity>(e =>
        {
            e.ToTable("LocalSessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.DeviceId).HasMaxLength(128);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.InstallationId, x.IsActive });
        });

        modelBuilder.Entity<LocalUserEntity>(e =>
        {
            e.ToTable("LocalUsers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(150);
            e.HasIndex(x => x.Email);
        });

        modelBuilder.Entity<LocalCompanyEntity>(e =>
        {
            e.ToTable("LocalCompanies");
            e.HasKey(x => x.Id);
            e.Property(x => x.CompanyName).HasMaxLength(200);
        });

        modelBuilder.Entity<LocalUserCompanyEntity>(e =>
        {
            e.ToTable("LocalUserCompanies");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.CompanyId }).IsUnique();
        });
        modelBuilder.Entity<LocalCompanyRoleEntity>(e=>{e.ToTable("LocalCompanyRoles");e.HasKey(x=>x.Id);e.HasIndex(x=>new{x.CompanyId,x.Name}).IsUnique();});
        modelBuilder.Entity<LocalCompanyRolePermissionEntity>(e=>{e.ToTable("LocalCompanyRolePermissions");e.HasKey(x=>x.Id);e.HasIndex(x=>new{x.CompanyId,x.RoleId,x.PermissionKey}).IsUnique();});

        modelBuilder.Entity<LocalRoleEntity>(e =>
        {
            e.ToTable("LocalRoles");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<LocalPermissionEntity>(e =>
        {
            e.ToTable("LocalPermissions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.CompanyId, x.PermissionName });
        });

        modelBuilder.Entity<LocalSubscriptionEntity>(e =>
        {
            e.ToTable("LocalSubscriptions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CompanyId);
            e.Property(x => x.PlanName).HasMaxLength(64);
        });

        modelBuilder.Entity<LocalLicenseEntity>(e =>
        {
            e.ToTable("LocalLicenses");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.LicenseId).IsUnique();
            e.HasIndex(x => x.DeviceId);
            e.Property(x => x.DeviceId).HasMaxLength(128);
            e.Property(x => x.Plan).HasMaxLength(64);
        });

        modelBuilder.Entity<LocalCustomerEntity>(e =>
        {
            e.ToTable("LocalCustomers");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CompanyId);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(150);
        });

        modelBuilder.Entity<LocalProductEntity>(e =>
        {
            e.ToTable("LocalProducts");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CompanyId);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.SKU).HasMaxLength(64);
        });

        modelBuilder.Entity<LocalStockMovementEntity>(e =>
        {
            e.ToTable("LocalStockMovements");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CompanyId, x.CreatedAtUtc });
            e.HasIndex(x => new { x.CompanyId, x.ProductId });
            e.Property(x => x.ProductName).HasMaxLength(200);
            e.Property(x => x.SKU).HasMaxLength(64);
        });

        modelBuilder.Entity<LocalSupplierEntity>(e =>
        {
            e.ToTable("LocalSuppliers");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CompanyId, x.Name });
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(150);
        });

        modelBuilder.Entity<LocalPurchaseEntity>(e =>
        {
            e.ToTable("LocalPurchases");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CompanyId, x.PurchaseNumber }).IsUnique();
            e.HasIndex(x => new { x.CompanyId, x.SupplierId });
            e.Property(x => x.PurchaseNumber).HasMaxLength(64);
        });

        modelBuilder.Entity<LocalExpenseEntity>(e =>
        {
            e.ToTable("LocalExpenses");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CompanyId, x.ExpenseDate });
            e.Property(x => x.Category).HasMaxLength(100);
            e.Property(x => x.Reference).HasMaxLength(100);
        });
        modelBuilder.Entity<LocalSalesReturnEntity>(e => { e.ToTable("LocalSalesReturns"); e.HasKey(x=>x.Id); e.HasIndex(x=>new{x.CompanyId,x.ReturnNumber}).IsUnique(); e.HasIndex(x=>new{x.CompanyId,x.InvoiceId}); });
        modelBuilder.Entity<LocalPurchaseReturnEntity>(e => { e.ToTable("LocalPurchaseReturns"); e.HasKey(x=>x.Id); e.HasIndex(x=>new{x.CompanyId,x.ReturnNumber}).IsUnique(); e.HasIndex(x=>new{x.CompanyId,x.PurchaseId}); });

        modelBuilder.Entity<LocalInvoiceEntity>(e =>
        {
            e.ToTable("LocalInvoices");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CompanyId);
            e.Property(x => x.InvoiceNumber).HasMaxLength(64);
        });

        modelBuilder.Entity<LocalPaymentEntity>(e =>
        {
            e.ToTable("LocalPayments");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CompanyId);
            e.Property(x => x.PaymentNumber).HasMaxLength(64);
        });

        modelBuilder.Entity<LocalSyncQueueEntity>(e =>
        {
            e.ToTable("LocalSyncQueue");
            e.HasKey(x => x.Id);
            e.Property(x => x.EntityType).HasMaxLength(128);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        modelBuilder.Entity<LocalSchemaInfoEntity>(e =>
        {
            e.ToTable("LocalSchemaInfo");
            e.HasKey(x => x.Id);
        });
    }
}
