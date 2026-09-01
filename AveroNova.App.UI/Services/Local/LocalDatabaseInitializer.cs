using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services.Local;

/// <summary>
/// Creates/updates the local AppDbContext SQLite schema and seeds only the
/// reference data required by the application. Optional demo-data failures
/// must never prevent login, registration, or password reset.
/// </summary>
public sealed class LocalDatabaseInitializer
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _ready;

    public static readonly Guid AdministratorRoleId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");

    private static readonly (string Name, string Description)[] Permissions =
    [
        (PermissionNames.DashboardView, "View Dashboard"),
        (PermissionNames.BillingView, "View Invoices"),
        (PermissionNames.BillingCreate, "Create Invoices"),
        (PermissionNames.BillingDelete, "Delete Invoices"),
        (PermissionNames.CustomersView, "View Customers"),
        (PermissionNames.CustomersManage, "Manage Customers"),
        (PermissionNames.ProductsView, "View Products"),
        (PermissionNames.ProductsManage, "Manage Products"),
        (PermissionNames.InventoryView, "View Inventory"),
        (PermissionNames.InventoryManage, "Manage Inventory"),
        (PermissionNames.PaymentsView, "View Payments"),
        (PermissionNames.PaymentsManage, "Manage Payments"),
        (PermissionNames.ReportsView, "View Reports"),
        (PermissionNames.UsersView, "View Users"),
        (PermissionNames.UsersCreate, "Create Users"),
        (PermissionNames.UsersUpdate, "Update Users"),
        (PermissionNames.UsersDelete, "Delete Users"),
        (PermissionNames.UsersAssignRole, "Assign User Roles"),
        (PermissionNames.UsersManage, "Manage Users"),
        (PermissionNames.SettingsManage, "Manage Settings"),
        (PermissionNames.SubscriptionView, "View Subscription"),
        (PermissionNames.CompanyView, "View Company"),
        (PermissionNames.CompanyUpdate, "Update Company"),
        (PermissionNames.PurchasesView, "View Purchases"),
        (PermissionNames.PurchasesManage, "Manage Purchases"),
        (PermissionNames.ExpensesView, "View Expenses")
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
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                AveroNova.App.UI.Helpers.StartupLog.Write("DB creating");
                await db.Database.EnsureCreatedAsync(cancellationToken);
            }
            else
            {
                AveroNova.App.UI.Helpers.StartupLog.Write("DB already present, skipping migrate");
            }

            AveroNova.App.UI.Helpers.StartupLog.Write("DB schema ensure start");
            await SqliteSubscriptionSchema.EnsureAsync(db, cancellationToken);
            await SqliteUserRoleSchema.EnsureAsync(db, cancellationToken);
            await SqliteCustomerSchema.EnsureAsync(db, cancellationToken);
            await SqliteProductSchema.EnsureAsync(db, cancellationToken);
            await SqliteUserSchema.EnsureAsync(db, cancellationToken);
            await SqliteInvoiceSchema.EnsureAsync(db, cancellationToken);
            await SqlitePurchaseSchema.EnsureAsync(db, cancellationToken);
            await SqlitePaymentSchema.EnsureAsync(db, cancellationToken);

            // These catalog/reference rows are required by account creation and access control.
            AveroNova.App.UI.Helpers.StartupLog.Write("DB required seed start");
            await SeedAsync(db, cancellationToken);
            await RoleCatalogSeeder.SeedAsync(db, cancellationToken);
            await SubscriptionCatalogSeeder.SeedAsync(db, cancellationToken);

            // Demo data is optional. A stale demo row/schema mismatch must not make the
            // authentication database look unavailable to Login/Register/Reset Password.
            try
            {
                AveroNova.App.UI.Helpers.StartupLog.Write("DB optional demo seed start");
                await DemoDataSeeder.SeedAsync(db, cancellationToken);
            }
            catch (Exception ex)
            {
                var root = ex;
                while (root.InnerException is not null)
                    root = root.InnerException;

                System.Diagnostics.Debug.WriteLine(
                    $"[swapdigit] Optional demo seed skipped. Type={ex.GetType().FullName}; " +
                    $"Message={ex.Message}; RootType={root.GetType().FullName}; RootMessage={root.Message}; " +
                    $"Stack={ex.StackTrace}");
                AveroNova.App.UI.Helpers.StartupLog.Write(
                    $"Optional demo seed skipped: {root.GetType().Name}: {root.Message}");

                // DemoDataSeeder may have tracked failed entities on this context. They are
                // irrelevant after initialization, so clear them before the context is disposed.
                db.ChangeTracker.Clear();
            }

            _ready = true;
            System.Diagnostics.Debug.WriteLine($"[swapdigit] Local SQLite ready: {db.Database.GetDbConnection().DataSource}");
        }
        catch (Exception ex)
        {
            var root = ex;
            while (root.InnerException is not null)
                root = root.InnerException;

            System.Diagnostics.Debug.WriteLine(
                $"[swapdigit] Local SQLite initialization FAILED. Type={ex.GetType().FullName}; " +
                $"Message={ex.Message}; RootType={root.GetType().FullName}; RootMessage={root.Message}; " +
                $"Stack={ex.StackTrace}");
            throw;
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
