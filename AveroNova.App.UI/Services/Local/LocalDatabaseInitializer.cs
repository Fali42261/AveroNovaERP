using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services.Local;

/// <summary>
/// Creates/updates the local AppDbContext SQLite schema. Authentication-critical
/// schema/reference data is required; unrelated ERP schema repair is best-effort
/// so a stale business table can never block Login, Create Account, or Reset Password.
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
            var dbPath = db.Database.GetDbConnection().DataSource;

            AveroNova.App.UI.Helpers.StartupLog.Write($"DB auth initialization start path={dbPath}");
            await db.Database.EnsureCreatedAsync(cancellationToken);

            // AUTH-CRITICAL SCHEMA. These failures must remain visible because authentication
            // genuinely cannot work without these tables/columns.
            AveroNova.App.UI.Helpers.StartupLog.Write("DB auth schema ensure start");
            await SqliteUserSchema.EnsureAsync(db, cancellationToken);
            await SqliteSubscriptionSchema.EnsureAsync(db, cancellationToken);
            await SqliteUserRoleSchema.EnsureAsync(db, cancellationToken);

            // AUTH-CRITICAL REFERENCE DATA used by registration and authorization.
            AveroNova.App.UI.Helpers.StartupLog.Write("DB auth reference seed start");
            await SeedAsync(db, cancellationToken);
            await RoleCatalogSeeder.SeedAsync(db, cancellationToken);
            await SubscriptionCatalogSeeder.SeedAsync(db, cancellationToken);

            // ERP/business schema repair is intentionally best-effort. A stale Customer,
            // Product, Invoice, Purchase or Payment table must not make the account database
            // appear unavailable on the authentication screens.
            await RunOptionalSchemaAsync(db, "Customer", () => SqliteCustomerSchema.EnsureAsync(db, cancellationToken));
            await RunOptionalSchemaAsync(db, "Product", () => SqliteProductSchema.EnsureAsync(db, cancellationToken));
            await RunOptionalSchemaAsync(db, "Invoice", () => SqliteInvoiceSchema.EnsureAsync(db, cancellationToken));
            await RunOptionalSchemaAsync(db, "Purchase", () => SqlitePurchaseSchema.EnsureAsync(db, cancellationToken));
            await RunOptionalSchemaAsync(db, "Payment", () => SqlitePaymentSchema.EnsureAsync(db, cancellationToken));

            // Do not auto-seed transactional/demo records into a real user's company here.
            // Account creation must persist only the records explicitly created by registration.

            _ready = true;
            System.Diagnostics.Debug.WriteLine($"[swapdigit] Local SQLite auth ready: {dbPath}");
            AveroNova.App.UI.Helpers.StartupLog.Write($"DB auth initialization complete path={dbPath}");
        }
        catch (Exception ex)
        {
            var root = GetRoot(ex);
            System.Diagnostics.Debug.WriteLine(
                $"[swapdigit] Local SQLite initialization FAILED. Type={ex.GetType().FullName}; " +
                $"Message={ex.Message}; RootType={root.GetType().FullName}; RootMessage={root.Message}; " +
                $"Stack={ex.StackTrace}");
            AveroNova.App.UI.Helpers.StartupLog.Write(
                $"Local SQLite initialization FAILED: {root.GetType().Name}: {root.Message}");
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task RunOptionalSchemaAsync(AppDbContext db, string name, Func<Task> ensure)
    {
        try
        {
            await ensure();
        }
        catch (Exception ex)
        {
            var root = GetRoot(ex);
            System.Diagnostics.Debug.WriteLine(
                $"[swapdigit] Optional {name} SQLite schema repair skipped. " +
                $"Type={root.GetType().FullName}; Message={root.Message}");
            AveroNova.App.UI.Helpers.StartupLog.Write(
                $"Optional {name} schema repair skipped: {root.GetType().Name}: {root.Message}");

            // A failed schema helper may leave tracked entities in an unusable state.
            // None of these entities are needed by authentication.
            db.ChangeTracker.Clear();
        }
    }

    private static Exception GetRoot(Exception ex)
    {
        var root = ex;
        while (root.InnerException is not null)
            root = root.InnerException;
        return root;
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
