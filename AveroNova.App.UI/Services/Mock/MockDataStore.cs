using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Mock;

// ═══════════════════════════════════════════════════════════════
//  MockDataStore — shared in-memory store for UI-phase mocks.
//  All mock services read/write from this static store so
//  navigation between pages sees consistent data.
// ═══════════════════════════════════════════════════════════════

public static class MockDataStore
{
    public static readonly Guid CompanyId1 = Guid.Parse("11111111-0000-0000-0000-000000000001");
    public static readonly Guid CompanyId2 = Guid.Parse("11111111-0000-0000-0000-000000000002");

    public static List<CompanyModel> Companies { get; } = new()
    {
        new() { LocalId = CompanyId1, Name = "AveroNova Global Ltd", Email = "info@averonova.com",
                Phone = "+1 555-0100", Address = "123 Business Ave", City = "New York",
                Country = "United States", TaxNumber = "TAX-001", Currency = "USD", CurrencySymbol = "$",
                InvoicePrefix = "INV", IsCurrentCompany = true, Status = CompanyStatus.Active,
                SyncStatus = SyncStatus.Synced },
        new() { LocalId = CompanyId2, Name = "AveroNova Asia Pte Ltd", Email = "asia@averonova.com",
                Phone = "+65 9000-1234", Address = "1 Tech Park", City = "Singapore",
                Country = "Singapore", TaxNumber = "TAX-SG-002", Currency = "SGD", CurrencySymbol = "S$",
                InvoicePrefix = "SGD-INV", Status = CompanyStatus.Active, SyncStatus = SyncStatus.Synced }
    };

    public static List<CustomerModel> Customers { get; } = new()
    {
        new() { LocalId = Guid.NewGuid(), Name = "John Smith", Email = "john.smith@email.com",
                Phone = "+1 555-1001", City = "Boston", Country = "US",
                Status = CustomerStatus.Active, OutstandingBalance = 1250.00m, TotalPurchases = 15420.00m,
                CompanyId = CompanyId1, SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), Name = "Sarah Johnson", Email = "sarah.j@corp.com",
                Phone = "+1 555-1002", City = "Chicago", Country = "US",
                Status = CustomerStatus.Active, OutstandingBalance = 0m, TotalPurchases = 8900.00m,
                CompanyId = CompanyId1, SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), Name = "TechCorp Solutions", Email = "accounts@techcorp.com",
                Phone = "+1 555-1003", City = "San Francisco", Country = "US",
                Status = CustomerStatus.Active, OutstandingBalance = 4500.00m, TotalPurchases = 52000.00m,
                CompanyId = CompanyId1, SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), Name = "Maria Garcia", Email = "maria.garcia@mail.com",
                Phone = "+1 555-1004", City = "Miami", Country = "US",
                Status = CustomerStatus.Inactive, OutstandingBalance = 230.00m, TotalPurchases = 3200.00m,
                CompanyId = CompanyId1, SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), Name = "BuildRight Inc.", Email = "payments@buildright.com",
                Phone = "+1 555-1005", City = "Dallas", Country = "US",
                Status = CustomerStatus.Active, OutstandingBalance = 9800.00m, TotalPurchases = 78000.00m,
                CompanyId = CompanyId1, SyncStatus = SyncStatus.PendingSync },
    };

    public static List<ProductModel> Products { get; } = new()
    {
        new() { LocalId = Guid.NewGuid(), Name = "Office Desk Pro", SKU = "DESK-001",
                Category = "Furniture", Brand = "OfficePlus", Unit = "pcs",
                PurchasePrice = 280m, SellingPrice = 450m, TaxPercent = 10m,
                Stock = 25, MinimumStock = 5, Status = ProductStatus.Active,
                CompanyId = CompanyId1, SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), Name = "Ergonomic Chair", SKU = "CHAIR-002",
                Category = "Furniture", Brand = "ComfortSeat", Unit = "pcs",
                PurchasePrice = 150m, SellingPrice = 280m, TaxPercent = 10m,
                Stock = 3, MinimumStock = 5, Status = ProductStatus.Active,
                CompanyId = CompanyId1, SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), Name = "Laptop Stand", SKU = "STAND-003",
                Category = "Electronics", Brand = "TechGear", Unit = "pcs",
                PurchasePrice = 25m, SellingPrice = 55m, TaxPercent = 10m,
                Stock = 50, MinimumStock = 10, Status = ProductStatus.Active,
                CompanyId = CompanyId1, SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), Name = "Wireless Mouse", SKU = "MOUSE-004",
                Category = "Electronics", Brand = "LogiPro", Unit = "pcs",
                PurchasePrice = 18m, SellingPrice = 45m, TaxPercent = 10m,
                Stock = 2, MinimumStock = 10, Status = ProductStatus.Active,
                CompanyId = CompanyId1, SyncStatus = SyncStatus.PendingSync },
        new() { LocalId = Guid.NewGuid(), Name = "USB-C Hub 7-in-1", SKU = "HUB-005",
                Category = "Electronics", Brand = "TechGear", Unit = "pcs",
                PurchasePrice = 22m, SellingPrice = 65m, TaxPercent = 10m,
                Stock = 30, MinimumStock = 8, Status = ProductStatus.Active,
                CompanyId = CompanyId1, SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), Name = "Monitor 27\" 4K", SKU = "MON-006",
                Category = "Electronics", Brand = "ViewMax", Unit = "pcs",
                PurchasePrice = 320m, SellingPrice = 580m, TaxPercent = 10m,
                Stock = 8, MinimumStock = 3, Status = ProductStatus.Active,
                CompanyId = CompanyId1, SyncStatus = SyncStatus.Synced },
    };

    public static List<UserModel> Users { get; } = new()
    {
        new() { LocalId = Guid.NewGuid(), Name = "Admin User", Email = "admin@averonova.com",
                Phone = "+1 555-9001", Role = "Administrator", AvatarInitials = "AU",
                Status = UserStatus.Active, CompanyId = CompanyId1, LastLoginAt = DateTime.UtcNow,
                SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), Name = "James Wilson", Email = "james.w@averonova.com",
                Phone = "+1 555-9002", Role = "Sales Manager", AvatarInitials = "JW",
                Status = UserStatus.Active, CompanyId = CompanyId1, LastLoginAt = DateTime.UtcNow.AddHours(-3),
                SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), Name = "Linda Chen", Email = "linda.c@averonova.com",
                Phone = "+1 555-9003", Role = "Accountant", AvatarInitials = "LC",
                Status = UserStatus.Active, CompanyId = CompanyId1, LastLoginAt = DateTime.UtcNow.AddDays(-1),
                SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), Name = "Robert Brown", Email = "robert.b@averonova.com",
                Phone = "+1 555-9004", Role = "Inventory Manager", AvatarInitials = "RB",
                Status = UserStatus.Inactive, CompanyId = CompanyId1, LastLoginAt = DateTime.UtcNow.AddDays(-7),
                SyncStatus = SyncStatus.Synced },
    };

    public static List<InvoiceModel> Invoices { get; } = new()
{
    new()
    {
        LocalId = Guid.NewGuid(),
        InvoiceNumber = "INV-2026-001",
        CustomerName = "TechCorp Solutions",
        InvoiceDate = DateTime.Today.AddDays(-5),
        DueDate = DateTime.Today.AddDays(25),
        PaidAmount = 0m,
        Status = InvoiceStatus.Sent,
        CompanyId = CompanyId1,
        SyncStatus = SyncStatus.Synced
    },

    new()
    {
        LocalId = Guid.NewGuid(),
        InvoiceNumber = "INV-2026-002",
        CustomerName = "John Smith",
        InvoiceDate = DateTime.Today.AddDays(-12),
        DueDate = DateTime.Today.AddDays(-2),
        PaidAmount = 0m,
        Status = InvoiceStatus.Paid,
        CompanyId = CompanyId1,
        SyncStatus = SyncStatus.Synced
    },

    new()
    {
        LocalId = Guid.NewGuid(),
        InvoiceNumber = "INV-2026-003",
        CustomerName = "BuildRight Inc.",
        InvoiceDate = DateTime.Today.AddDays(-35),
        DueDate = DateTime.Today.AddDays(-5),
        PaidAmount = 0m,
        Status = InvoiceStatus.Overdue,
        CompanyId = CompanyId1,
        SyncStatus = SyncStatus.Synced
    },

    new()
    {
        LocalId = Guid.NewGuid(),
        InvoiceNumber = "INV-2026-004",
        CustomerName = "Sarah Johnson",
        InvoiceDate = DateTime.Today,
        DueDate = DateTime.Today.AddDays(30),
        PaidAmount = 0m,
        Status = InvoiceStatus.Draft,
        CompanyId = CompanyId1,
        SyncStatus = SyncStatus.PendingSync
    }
};
    public static List<PaymentModel> Payments { get; } = new()
    {
        new() { LocalId = Guid.NewGuid(), PaymentNumber = "PAY-2026-001",
                PartyName = "John Smith", InvoiceNumber = "INV-2026-002",
                Amount = 1250.00m, Method = PaymentMethod.BankTransfer,
                PaymentDate = DateTime.Today.AddDays(-2), Status = PaymentStatus.Completed,
                CompanyId = CompanyId1, SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), PaymentNumber = "PAY-2026-002",
                PartyName = "TechCorp Solutions", InvoiceNumber = "INV-2026-001",
                Amount = 1500.00m, Method = PaymentMethod.Cash,
                PaymentDate = DateTime.Today, Status = PaymentStatus.Completed,
                CompanyId = CompanyId1, SyncStatus = SyncStatus.PendingSync },
    };

    public static List<ExpenseModel> Expenses { get; } = new()
    {
        new() { LocalId = Guid.NewGuid(), Category = "Office Supplies", Description = "Printer paper & stationery",
                Amount = 145.50m, ExpenseDate = DateTime.Today.AddDays(-3),
                Method = PaymentMethod.Cash, Status = ExpenseStatus.Approved,
                CompanyId = CompanyId1, SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), Category = "Travel", Description = "Client site visit",
                Amount = 380.00m, ExpenseDate = DateTime.Today.AddDays(-8),
                Method = PaymentMethod.CreditCard, Status = ExpenseStatus.Paid,
                CompanyId = CompanyId1, SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), Category = "Software", Description = "Annual SaaS subscription",
                Amount = 1200.00m, ExpenseDate = DateTime.Today.AddDays(-1),
                Method = PaymentMethod.CreditCard, Status = ExpenseStatus.Pending,
                CompanyId = CompanyId1, SyncStatus = SyncStatus.PendingSync },
    };

    public static List<RoleModel> Roles { get; } = new()
    {
        new() { LocalId = Guid.NewGuid(), Name = "Administrator", Description = "Full system access",
                IsSystem = true, UserCount = 1, CompanyId = CompanyId1,
                Permissions = ["*"], SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), Name = "Sales Manager", Description = "Manage sales, billing, customers",
                IsSystem = false, UserCount = 2, CompanyId = CompanyId1,
                Permissions = ["billing.view","billing.create","customers.view","customers.manage"],
                SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), Name = "Accountant", Description = "Payments, reports, expenses",
                IsSystem = false, UserCount = 1, CompanyId = CompanyId1,
                Permissions = ["payments.view","payments.manage","reports.view","expenses.manage"],
                SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), Name = "Inventory Manager", Description = "Products & inventory",
                IsSystem = false, UserCount = 1, CompanyId = CompanyId1,
                Permissions = ["products.view","products.manage","inventory.view","inventory.manage"],
                SyncStatus = SyncStatus.Synced },
    };

    public static List<SyncHistoryModel> SyncHistory { get; } = new()
    {
        new() { SyncedAt = DateTime.UtcNow.AddHours(-1), Success = true,  ItemsSynced = 12, Module = "All Modules", Message = "Sync completed successfully." },
        new() { SyncedAt = DateTime.UtcNow.AddHours(-4), Success = false, ItemsSynced = 0,  Module = "Billing",      Message = "Network timeout. Retrying..." },
        new() { SyncedAt = DateTime.UtcNow.AddDays(-1),  Success = true,  ItemsSynced = 8,  Module = "All Modules", Message = "Sync completed successfully." },
        new() { SyncedAt = DateTime.UtcNow.AddDays(-2),  Success = true,  ItemsSynced = 25, Module = "All Modules", Message = "Sync completed successfully." },
    };
}
