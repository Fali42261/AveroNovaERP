using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services.Local;

/// <summary>
/// Creates/migrates the existing AppDbContext SQLite schema and seeds
/// Administrator + permissions. Does not invent tables.
/// </summary>
public sealed class LocalDatabaseInitializer
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _ready;

    public static readonly Guid AdministratorRoleId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");

    private static readonly (string Name, string Description)[] Permissions =
    [
        ("dashboard.view", "View Dashboard"),
        ("billing.view", "View Invoices"),
        ("billing.create", "Create Invoices"),
        ("billing.delete", "Delete Invoices"),
        ("customers.view", "View Customers"),
        ("customers.manage", "Manage Customers"),
        ("products.view", "View Products"),
        ("products.manage", "Manage Products"),
        ("inventory.view", "View Inventory"),
        ("inventory.manage", "Manage Inventory"),
        ("payments.view", "View Payments"),
        ("payments.manage", "Manage Payments"),
        ("reports.view", "View Reports"),
        ("users.manage", "Manage Users"),
        ("settings.manage", "Manage Settings"),
        ("subscription.view", "View Subscription")
    ];

    public LocalDatabaseInitializer(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_ready)
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_ready)
                return;

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AveroNova] Migrate failed, EnsureCreated: {ex.Message}");
                await db.Database.EnsureCreatedAsync(cancellationToken);
            }

            await SeedAsync(db, cancellationToken);
            _ready = true;
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Local SQLite ready: {db.Database.GetDbConnection().DataSource}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var admin = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator", cancellationToken);
        if (admin == null)
        {
            admin = new Role
            {
                Id = AdministratorRoleId,
                Name = "Administrator",
                Description = "Full system access",
                CreatedAt = now,
                IsDeleted = false
            };
            db.Roles.Add(admin);
            await db.SaveChangesAsync(cancellationToken);
        }

        foreach (var (name, description) in Permissions)
        {
            var existing = await db.Permissions.FirstOrDefaultAsync(p => p.PermissionName == name, cancellationToken);
            if (existing == null)
            {
                existing = new Permission
                {
                    Id = Guid.NewGuid(),
                    PermissionName = name,
                    Description = description,
                    CreatedAt = now,
                    IsDeleted = false
                };
                db.Permissions.Add(existing);
                await db.SaveChangesAsync(cancellationToken);
            }

            var assigned = await db.RolePermissions.AnyAsync(
                rp => rp.RoleId == admin.Id && rp.PermissionId == existing.Id,
                cancellationToken);
            if (!assigned)
            {
                db.RolePermissions.Add(new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = admin.Id,
                    PermissionId = existing.Id,
                    CreatedAt = now,
                    IsDeleted = false
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
