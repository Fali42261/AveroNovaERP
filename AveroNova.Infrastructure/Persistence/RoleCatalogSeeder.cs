using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Persistence
{
    public static class RoleCatalogSeeder
    {
        public static readonly Guid AdministratorRoleId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
        public static readonly Guid HrRoleId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000002");
        public static readonly Guid ManagerRoleId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000003");
        public static readonly Guid SalesRoleId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000004");
        public static readonly Guid InventoryRoleId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000005");
        public static readonly Guid AccountantRoleId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000006");
        public static readonly Guid CustomRoleId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000007");

        public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            await EnsureRoleAsync(db, AdministratorRoleId, RoleNames.Administrator, "Full system access", now, cancellationToken);
            await EnsureRoleAsync(db, HrRoleId, RoleNames.HR, "Human resources and user administration", now, cancellationToken);
            await EnsureRoleAsync(db, ManagerRoleId, RoleNames.Manager, "Operational management and reporting", now, cancellationToken);
            await EnsureRoleAsync(db, SalesRoleId, RoleNames.Sales, "Customers and sales", now, cancellationToken);
            await EnsureRoleAsync(db, InventoryRoleId, RoleNames.Inventory, "Products and inventory", now, cancellationToken);
            await EnsureRoleAsync(db, AccountantRoleId, RoleNames.Accountant, "Payments, expenses, and reports", now, cancellationToken);
            await EnsureRoleAsync(db, CustomRoleId, RoleNames.CustomRole, "Customizable company role", now, cancellationToken);

            await AssignAsync(db, HrRoleId, now, cancellationToken,
                PermissionNames.DashboardView,
                PermissionNames.CompanyView,
                PermissionNames.UsersView,
                PermissionNames.UsersCreate,
                PermissionNames.UsersUpdate,
                PermissionNames.UsersAssignRole);

            await AssignAsync(db, ManagerRoleId, now, cancellationToken,
                PermissionNames.DashboardView,
                PermissionNames.CompanyView,
                PermissionNames.CustomersView,
                PermissionNames.ProductsView,
                PermissionNames.InventoryView,
                PermissionNames.BillingView,
                PermissionNames.ReportsView,
                PermissionNames.UsersView);

            await AssignAsync(db, SalesRoleId, now, cancellationToken,
                PermissionNames.DashboardView,
                PermissionNames.CustomersView,
                PermissionNames.CustomersManage,
                PermissionNames.BillingView,
                PermissionNames.BillingCreate);

            await AssignAsync(db, InventoryRoleId, now, cancellationToken,
                PermissionNames.DashboardView,
                PermissionNames.ProductsView,
                PermissionNames.ProductsManage,
                PermissionNames.InventoryView,
                PermissionNames.InventoryManage);

            await AssignAsync(db, AccountantRoleId, now, cancellationToken,
                PermissionNames.DashboardView,
                PermissionNames.BillingView,
                PermissionNames.PaymentsView,
                PermissionNames.PaymentsManage,
                PermissionNames.ExpensesView,
                PermissionNames.ReportsView);

            await AssignAsync(db, CustomRoleId, now, cancellationToken,
                PermissionNames.DashboardView);
        }

        private static async Task EnsureRoleAsync(
            AppDbContext db,
            Guid id,
            string name,
            string description,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var existing = await db.Roles.FirstOrDefaultAsync(
                r => r.Id == id || r.Name == name, cancellationToken);
            if (existing != null)
            {
                if (existing.IsDeleted)
                {
                    existing.IsDeleted = false;
                    existing.UpdatedAt = now;
                    await db.SaveChangesAsync(cancellationToken);
                }
                return;
            }

            db.Roles.Add(new Role
            {
                Id = id,
                Name = name,
                Description = description,
                CreatedAt = now,
                IsDeleted = false
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        private static async Task AssignAsync(
            AppDbContext db,
            Guid roleId,
            DateTime now,
            CancellationToken cancellationToken,
            params string[] permissionNames)
        {
            foreach (var permissionName in permissionNames)
            {
                var permission = await db.Permissions.FirstOrDefaultAsync(
                    p => p.PermissionName == permissionName, cancellationToken);
                if (permission == null)
                    continue;

                var assigned = await db.RolePermissions.AnyAsync(
                    rp => rp.RoleId == roleId && rp.PermissionId == permission.Id && !rp.IsDeleted,
                    cancellationToken);
                if (assigned)
                    continue;

                db.RolePermissions.Add(new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = roleId,
                    PermissionId = permission.Id,
                    CreatedAt = now,
                    IsDeleted = false
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
