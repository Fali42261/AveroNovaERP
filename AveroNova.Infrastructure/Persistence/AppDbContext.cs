using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<UserCompany> UserCompanies => Set<UserCompany>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<DeviceSession> DeviceSessions => Set<DeviceSession>();
    public DbSet<ClientInstallation> ClientInstallations => Set<ClientInstallation>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<SyncQueueItem> SyncQueueItems => Set<SyncQueueItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        SeedFoundationData(modelBuilder);
    }

    private static void SeedFoundationData(ModelBuilder modelBuilder)
    {
        var seedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var starter = Plan.CreateStarterCatalog(); starter.CreatedAt = seedAt;
        var business = Plan.CreateBusinessCatalog(); business.CreatedAt = seedAt;
        var enterprise = Plan.CreateEnterpriseCatalog(); enterprise.CreatedAt = seedAt;
        modelBuilder.Entity<Plan>().HasData(starter, business, enterprise);
        var ownerRoleId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        modelBuilder.Entity<Role>().HasData(new Role { Id = ownerRoleId, Name = Domain.Constants.RoleNames.CompanyOwner, Description = "Full access company owner role.", CreatedAt = seedAt, SyncStatus = RecordSyncStatus.Synced, SyncVersion = 1 });
        var permissions = new (Guid Id, Guid LinkId, string Name, string Description)[]
        {
            (Guid.Parse("b1111111-1111-1111-1111-111111111111"), Guid.Parse("c1111111-1111-1111-1111-111111111111"), "Dashboard.View", "View dashboard"),
            (Guid.Parse("b2222222-2222-2222-2222-222222222222"), Guid.Parse("c2222222-2222-2222-2222-222222222222"), "Sales.View", "View sales"),
            (Guid.Parse("b3333333-3333-3333-3333-333333333333"), Guid.Parse("c3333333-3333-3333-3333-333333333333"), "Sales.Create", "Create sales"),
            (Guid.Parse("b4444444-4444-4444-4444-444444444444"), Guid.Parse("c4444444-4444-4444-4444-444444444444"), "Inventory.View", "View inventory"),
            (Guid.Parse("b5555555-5555-5555-5555-555555555555"), Guid.Parse("c5555555-5555-5555-5555-555555555555"), "Customers.View", "View customers"),
            (Guid.Parse("b6666666-6666-6666-6666-666666666666"), Guid.Parse("c6666666-6666-6666-6666-666666666666"), "Reports.View", "View reports"),
            (Guid.Parse("b7777777-7777-7777-7777-777777777777"), Guid.Parse("c7777777-7777-7777-7777-777777777777"), "Company.Manage", "Manage company"),
            (Guid.Parse("b8888888-8888-8888-8888-888888888888"), Guid.Parse("c8888888-8888-8888-8888-888888888888"), "Users.Manage", "Manage users"),
        };
        foreach (var p in permissions)
        {
            modelBuilder.Entity<Permission>().HasData(new Permission { Id = p.Id, PermissionName = p.Name, Description = p.Description, CreatedAt = seedAt, SyncStatus = RecordSyncStatus.Synced, SyncVersion = 1 });
            modelBuilder.Entity<RolePermission>().HasData(new RolePermission { Id = p.LinkId, RoleId = ownerRoleId, PermissionId = p.Id, CreatedAt = seedAt, SyncStatus = RecordSyncStatus.Synced, SyncVersion = 1 });
        }
    }
}
