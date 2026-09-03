using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Services;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services.Local;

/// <summary>
/// Seeds realistic demo data for the demo@gmail.com test account.
/// Idempotent: if demo records already exist for that company, does nothing.
/// </summary>
internal static class DemoDataSeeder
{
    private const string DemoEmail = "demo@gmail.com";
    private const string DemoPassword = "Demo@1234";
    private const string DemoCompanyName = "swapdigit Demo";

    // Fixed IDs so re-runs never duplicate
    private static readonly Guid DemoUserId    = Guid.Parse("dddddddd-0000-4000-8000-000000000001");
    private static readonly Guid DemoCompanyId = Guid.Parse("dddddddd-0000-4000-8000-000000000002");

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        // Idempotency check: if the demo company's customers already exist, skip.
        if (await db.Customers.AnyAsync(c => c.CompanyId == DemoCompanyId && !c.IsDeleted, ct))
            return;

        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // ── 1. User ─────────────────────────────────────────────────────
        await EnsureUserAsync(db, now, ct);

        // ── 2. Company ───────────────────────────────────────────────────
        await EnsureCompanyAsync(db, now, ct);

        // ── 3. Subscription (30-day trial starting now) ─────────────────
        await EnsureSubscriptionAsync(db, now, ct);

        // ── 4. UserRole ──────────────────────────────────────────────────
        await EnsureUserRoleAsync(db, now, ct);

        // ── 5. Products ──────────────────────────────────────────────────
        var productIds = await SeedProductsAsync(db, now, ct);

        // ── 6. Customers ─────────────────────────────────────────────────
        var customerIds = await SeedCustomersAsync(db, now, ct);

        // ── 7. Sales / Invoices ──────────────────────────────────────────
        var invoiceIds = await SeedInvoicesAsync(db, now, productIds, customerIds, ct);

        // ── 8. Payments ──────────────────────────────────────────────────
        await SeedPaymentsAsync(db, now, invoiceIds, customerIds, ct);

        // ── 9. Purchases ─────────────────────────────────────────────────
        await SeedPurchasesAsync(db, now, productIds, ct);

        // ── 10. Stock Movements ──────────────────────────────────────────
        await SeedStockMovementsAsync(db, now, productIds, ct);

        await db.SaveChangesAsync(ct);

        System.Diagnostics.Debug.WriteLine(
            "[swapdigit] Demo data seeded for demo@gmail.com / company=" + DemoCompanyId);
    }

    // ─────────────────────────────────────────────────────────────────────
    private static async Task EnsureUserAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Id == DemoUserId, ct))
            return;

        db.Users.Add(new User
        {
            Id = DemoUserId,
            UserCode = "UDEMO001",
            FullName = "Demo User",
            Email = DemoEmail,
            PasswordHash = LocalPasswordHasher.Hash(DemoPassword),
            IsActiveUser = true,
            CreatedAt = now,
            IsDeleted = false
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureCompanyAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.Companies.AnyAsync(c => c.Id == DemoCompanyId, ct))
            return;

        db.Companies.Add(new Company
        {
            Id = DemoCompanyId,
            UserId = DemoUserId,
            CompanyCode = "CDEMO001",
            CompanyName = DemoCompanyName,
            OwnerName = "Demo User",
            Email = DemoEmail,
            MobileNumber = "9000000000",
            Address = "123 Demo Street",
            City = "Mumbai",
            State = "Maharashtra",
            Country = "India",
            PinCode = "400001",
            CreatedAt = now,
            IsDeleted = false
        });

        db.UserCompanies.Add(new UserCompany
        {
            Id = Guid.Parse("dddddddd-0000-4000-8000-000000000003"),
            UserId = DemoUserId,
            CompanyId = DemoCompanyId,
            IsOwner = true,
            IsActive = true,
            CreatedAt = now,
            IsDeleted = false
        });

        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureSubscriptionAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.Subscriptions.AnyAsync(s => s.CompanyId == DemoCompanyId && !s.IsDeleted, ct))
            return;

        var plan = await db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Code == SubscriptionPlanCodes.FreeTrial && !p.IsDeleted, ct);
        if (plan == null)
            return;

        var sub = FreeTrialSubscriptionFactory.Create(DemoCompanyId, plan, now);
        // Extend demo subscription so it doesn't expire during testing
        sub.ExpiryDate = now.AddDays(3650);
        sub.TrialEndDate = now.AddDays(3650);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureUserRoleAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.UserRoles.AnyAsync(ur => ur.UserId == DemoUserId && ur.CompanyId == DemoCompanyId && !ur.IsDeleted, ct))
            return;

        var admin = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator" && !r.IsDeleted, ct);
        if (admin == null)
            return;

        db.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = DemoUserId,
            RoleId = admin.Id,
            CompanyId = DemoCompanyId,
            CreatedAt = now,
            IsDeleted = false
        });
        await db.SaveChangesAsync(ct);
    }

    // ─── Products ────────────────────────────────────────────────────────
    private static async Task<List<(Guid Id, string Name, string Sku, decimal Price)>> SeedProductsAsync(
        AppDbContext db, DateTime now, CancellationToken ct)
    {
        var defs = new (string Name, string Sku, string Cat, decimal PurchasePrice, decimal SalePrice, int Stock, int MinStock)[]
        {
            ("Wireless Mouse",   "WM-001", "Peripherals", 350m,  599m,  45, 5),
            ("USB Keyboard",     "KB-002", "Peripherals", 450m,  799m,  30, 5),
            ("USB-C Cable 1m",   "UC-003", "Cables",       80m,  149m, 120, 10),
            ("HDMI Cable 1.5m",  "HC-004", "Cables",      120m,  199m,  80, 8),
            ("Power Adapter 65W","PA-005", "Adapters",    600m,  999m,  25, 4),
            ("Laptop Stand",     "LS-006", "Accessories", 700m, 1199m,  18, 3),
            ("Screen Cleaner Kit","CK-007","Accessories",  90m,  179m,  60, 6),
            ("Extension Board 4-Port","EB-008","Electrical",350m, 599m, 22, 4),
            ("Webcam HD 1080p",  "WC-009", "Peripherals",1200m, 1999m,  12, 2),
            ("Bluetooth Speaker","BS-010", "Audio",       900m, 1499m,   8, 2),
        };

        var result = new List<(Guid, string, string, decimal)>();
        foreach (var (name, sku, cat, pp, sp, stock, minStock) in defs)
        {
            var existing = await db.Products.FirstOrDefaultAsync(
                p => p.CompanyId == DemoCompanyId && p.SKU == sku && !p.IsDeleted, ct);
            if (existing != null)
            {
                result.Add((existing.Id, existing.Name, existing.SKU, existing.SellingPrice));
                continue;
            }

            var id = Guid.NewGuid();
            db.Products.Add(new Product
            {
                Id = id,
                CompanyId = DemoCompanyId,
                Name = name,
                SKU = sku,
                Category = cat,
                Brand = "swapdigit",
                Unit = "pcs",
                PurchasePrice = pp,
                SellingPrice = sp,
                TaxPercent = 18m,
                DiscountPercent = 0m,
                Stock = stock,
                OpeningStock = stock,
                MinimumStock = minStock,
                Status = 0, // Active
                CreatedAt = now,
                IsDeleted = false
            });
            result.Add((id, name, sku, sp));
        }
        await db.SaveChangesAsync(ct);
        return result;
    }

    // ─── Customers ───────────────────────────────────────────────────────
    private static async Task<List<Guid>> SeedCustomersAsync(
        AppDbContext db, DateTime now, CancellationToken ct)
    {
        var defs = new (string Name, string Mobile, string Email, string City)[]
        {
            ("Bright Traders",       "9811111111", "bright@traders.com",    "Delhi"),
            ("Metro Retail",         "9822222222", "metro@retail.com",       "Mumbai"),
            ("Prime Supplies",       "9833333333", "prime@supplies.com",     "Bangalore"),
            ("City Mart",            "9844444444", "info@citymart.com",      "Chennai"),
            ("Nova Stores",          "9855555555", "nova@stores.com",        "Hyderabad"),
            ("Greenline Enterprises","9866666666", "green@line.com",         "Pune"),
            ("Star Electronics",     "9877777777", "star@electronics.com",   "Kolkata"),
            ("Horizon Distributors", "9888888888", "horizon@distrib.com",    "Ahmedabad"),
        };

        var result = new List<Guid>();
        foreach (var (name, mobile, email, city) in defs)
        {
            var existing = await db.Customers.FirstOrDefaultAsync(
                c => c.CompanyId == DemoCompanyId && c.Name == name && !c.IsDeleted, ct);
            if (existing != null)
            {
                result.Add(existing.Id);
                continue;
            }

            var id = Guid.NewGuid();
            db.Customers.Add(new Customer
            {
                Id = id,
                CompanyId = DemoCompanyId,
                Name = name,
                MobileNumber = mobile,
                Email = email,
                City = city,
                State = "India",
                Country = "India",
                Status = 0, // Active
                CreatedAt = now,
                IsDeleted = false
            });
            result.Add(id);
        }
        await db.SaveChangesAsync(ct);
        return result;
    }

    // ─── Invoices ────────────────────────────────────────────────────────
    private static async Task<List<(Guid Id, string Number, decimal Total)>> SeedInvoicesAsync(
        AppDbContext db, DateTime now,
        List<(Guid Id, string Name, string Sku, decimal Price)> products,
        List<Guid> customers,
        CancellationToken ct)
    {
        // Status: 0=Draft, 1=Sent, 2=Paid, 3=PartialPaid, 4=Overdue, 5=Cancelled
        var defs = new (int custIdx, int status, DateTime date, DateTime due, int paymentMethod, decimal paidAmount, (int prodIdx, int qty)[] items)[]
        {
            (0, 2, now.AddDays(-30), now.AddDays(-20), 0, 0m,    new[]{(0,2),(2,5)}),          // Paid
            (1, 1, now.AddDays(-25), now.AddDays(-5),  1, 0m,    new[]{(1,1),(3,2)}),          // Sent
            (2, 3, now.AddDays(-20), now.AddDays(-10), 0, 500m,  new[]{(4,1),(5,1)}),          // Partial Paid
            (3, 4, now.AddDays(-45), now.AddDays(-15), 2, 0m,    new[]{(6,3),(2,10)}),         // Overdue
            (4, 0, now.AddDays(-5),  now.AddDays(10),  0, 0m,    new[]{(0,1),(1,1),(2,2)}),   // Draft
            (5, 2, now.AddDays(-60), now.AddDays(-50), 1, 0m,    new[]{(7,2),(8,1)}),          // Paid
            (6, 1, now.AddDays(-10), now.AddDays(5),   0, 0m,    new[]{(9,1),(3,1)}),          // Sent
            (7, 2, now.AddDays(-90), now.AddDays(-80), 3, 0m,    new[]{(0,3),(1,2),(4,1)}),   // Paid
        };

        var result = new List<(Guid, string, decimal)>();
        var invoiceCount = await db.Invoices.CountAsync(i => i.CompanyId == DemoCompanyId, ct);

        for (var i = 0; i < defs.Length; i++)
        {
            var d = defs[i];
            var invoiceNum = $"INV-DEMO-{(i + 1):D3}";

            if (await db.Invoices.AnyAsync(inv => inv.InvoiceNumber == invoiceNum && inv.CompanyId == DemoCompanyId, ct))
            {
                var ex = await db.Invoices.FirstAsync(inv => inv.InvoiceNumber == invoiceNum && inv.CompanyId == DemoCompanyId, ct);
                result.Add((ex.Id, ex.InvoiceNumber, ex.PaidAmount));
                continue;
            }

            var custId  = customers[d.custIdx % customers.Count];
            var custName = await db.Customers
                .Where(c => c.Id == custId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(ct) ?? "Customer";

            var invoiceId = Guid.NewGuid();
            var invoiceItems = new List<InvoiceItem>();
            decimal subtotal = 0;

            foreach (var (prodIdx, qty) in d.items)
            {
                var prod = products[prodIdx % products.Count];
                var lineTotal = prod.Price * qty;
                subtotal += lineTotal;
                invoiceItems.Add(new InvoiceItem
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    ProductId = prod.Id,
                    ProductName = prod.Name,
                    SKU = prod.Sku,
                    UnitPrice = prod.Price,
                    Quantity = qty,
                    TaxPct = 18m,
                    CreatedAt = d.date,
                    IsDeleted = false
                });
            }

            // For paid invoices, paidAmount = subtotal; partial uses supplied amount
            var paidAmount = d.status == 2 ? subtotal : d.paidAmount;

            db.Invoices.Add(new Invoice
            {
                Id = invoiceId,
                CompanyId = DemoCompanyId,
                InvoiceNumber = invoiceNum,
                CustomerId = custId,
                CustomerName = custName,
                InvoiceDate = d.date,
                DueDate = d.due,
                TaxPct = 18m,
                DiscountPct = 0m,
                PaymentMethod = d.paymentMethod,
                PaidAmount = paidAmount,
                Status = d.status,
                SyncStatus = 0,
                Items = invoiceItems,
                CreatedAt = d.date,
                IsDeleted = false
            });

            result.Add((invoiceId, invoiceNum, paidAmount));
        }

        await db.SaveChangesAsync(ct);
        return result;
    }

    // ─── Payments ────────────────────────────────────────────────────────
    private static async Task SeedPaymentsAsync(
        AppDbContext db, DateTime now,
        List<(Guid Id, string Number, decimal Total)> invoices,
        List<Guid> customers,
        CancellationToken ct)
    {
        var demoPayments = new[]
        {
            ("PAY-DEMO-001", false, 0, 0, 1198m,  now.AddDays(-29), "Customer payment - Bright Traders"),
            ("PAY-DEMO-002", false, 1, 1, 1997m,  now.AddDays(-58), "Customer payment - Metro Retail"),
            ("PAY-DEMO-003", false, 7, 5, 8994m,  now.AddDays(-88), "Customer payment - Horizon Distributors"),
            ("PAY-DEMO-004", true,  0, -1, 5000m, now.AddDays(-20), "Supplier payment - Tech Imports Ltd"),
            ("PAY-DEMO-005", false, 4, 2, 500m,   now.AddDays(-18), "Partial payment - Prime Supplies"),
        };

        foreach (var (num, isSupplier, custIdx, invIdx, amount, date, notes) in demoPayments)
        {
            if (await db.Payments.AnyAsync(p => p.PaymentNumber == num && p.CompanyId == DemoCompanyId, ct))
                continue;

            var partyId   = isSupplier ? Guid.NewGuid() : customers[custIdx % customers.Count];
            var partyName = isSupplier ? "Tech Imports Ltd" : (await db.Customers.Where(c => c.Id == partyId).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "Customer");
            Guid? invoiceId = null;
            string invoiceNumber = string.Empty;
            if (!isSupplier && invIdx >= 0 && invIdx < invoices.Count)
            {
                invoiceId     = invoices[invIdx].Id;
                invoiceNumber = invoices[invIdx].Number;
            }

            db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                CompanyId = DemoCompanyId,
                PaymentNumber = num,
                PartyId = partyId,
                PartyName = partyName,
                IsSupplier = isSupplier,
                InvoiceId = invoiceId,
                InvoiceNumber = invoiceNumber,
                Amount = amount,
                PaymentDate = date,
                Method = 0, // Cash
                Notes = notes,
                Status = 1, // Completed
                SyncStatus = 0,
                CreatedAt = date,
                IsDeleted = false
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // ─── Purchases ───────────────────────────────────────────────────────
    private static async Task SeedPurchasesAsync(
        AppDbContext db, DateTime now,
        List<(Guid Id, string Name, string Sku, decimal Price)> products,
        CancellationToken ct)
    {
        var defs = new (string Num, string Supplier, DateTime Date, int Status, (int prodIdx, int qty, decimal unitPrice)[] items)[]
        {
            ("PO-DEMO-001", "Tech Imports Ltd",   now.AddDays(-60), 2, new[]{(0,50, 350m),(1,30, 450m)}),
            ("PO-DEMO-002", "CableCo Supplies",   now.AddDays(-45), 2, new[]{(2,200, 80m),(3,100, 120m)}),
            ("PO-DEMO-003", "PowerPro Wholesale", now.AddDays(-30), 1, new[]{(4,30, 600m),(5,20, 700m)}),
            ("PO-DEMO-004", "Tech Imports Ltd",   now.AddDays(-15), 0, new[]{(6,80,  90m),(7,40, 350m)}),
            ("PO-DEMO-005", "AudioWorld",         now.AddDays(-7),  1, new[]{(8,20,1200m),(9,15, 900m)}),
        };

        foreach (var (num, supplier, date, status, items) in defs)
        {
            if (await db.Purchases.AnyAsync(p => p.PurchaseNumber == num && p.CompanyId == DemoCompanyId, ct))
                continue;

            var purchaseId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();
            var purchaseItems = items.Select(item =>
            {
                var prod = products[item.prodIdx % products.Count];
                return new PurchaseItem
                {
                    Id = Guid.NewGuid(),
                    PurchaseId = purchaseId,
                    ProductId = prod.Id,
                    ProductName = prod.Name,
                    SKU = prod.Sku,
                    UnitPrice = item.unitPrice,
                    Quantity = item.qty,
                    TaxPct = 18m,
                    CreatedAt = date,
                    IsDeleted = false
                };
            }).ToList();

            db.Purchases.Add(new Purchase
            {
                Id = purchaseId,
                CompanyId = DemoCompanyId,
                PurchaseNumber = num,
                SupplierId = supplierId,
                SupplierName = supplier,
                PurchaseDate = date,
                DueDate = date.AddDays(30),
                PaymentMethod = 1,
                PaidAmount = status == 2 ? purchaseItems.Sum(i => i.UnitPrice * i.Quantity) : 0m,
                Status = status,
                SyncStatus = 0,
                Items = purchaseItems,
                CreatedAt = date,
                IsDeleted = false
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // ─── Stock Movements ─────────────────────────────────────────────────
    private static async Task SeedStockMovementsAsync(
        AppDbContext db, DateTime now,
        List<(Guid Id, string Name, string Sku, decimal Price)> products,
        CancellationToken ct)
    {
        // Only seed if no movements exist for this company
        if (await db.StockMovements.AnyAsync(sm => sm.CompanyId == DemoCompanyId, ct))
            return;

        // MovementType: 1=Purchase(In), 2=Sale(Out), 3=Adjustment
        var movements = new[]
        {
            (0,  1, 50,  0,  50,  "PO-DEMO-001", now.AddDays(-60)),
            (1,  1, 30,  0,  30,  "PO-DEMO-001", now.AddDays(-60)),
            (2,  1, 200, 0,  200, "PO-DEMO-002", now.AddDays(-45)),
            (3,  1, 100, 0,  100, "PO-DEMO-002", now.AddDays(-45)),
            (4,  1, 30,  0,  30,  "PO-DEMO-003", now.AddDays(-30)),
            (5,  1, 20,  0,  20,  "PO-DEMO-003", now.AddDays(-30)),
            (0,  2, 5,   50, 45,  "INV-DEMO-001", now.AddDays(-30)),
            (2,  2, 10,  200,190, "INV-DEMO-001", now.AddDays(-30)),
            (1,  2, 1,   30, 29,  "INV-DEMO-002", now.AddDays(-25)),
            (3,  2, 2,   100,98,  "INV-DEMO-002", now.AddDays(-25)),
            (6,  2, 3,   0,  60,  "INV-DEMO-004", now.AddDays(-45)),
            (2,  2, 10,  190,180, "INV-DEMO-004", now.AddDays(-45)),
            // Low-stock adjustment
            (9,  3, 8,   0,  8,   "OPENING", now.AddDays(-90)),
            (8,  3, 12,  0,  12,  "OPENING", now.AddDays(-90)),
        };

        foreach (var (prodIdx, movType, qty, before, after, reference, date) in movements)
        {
            var prod = products[prodIdx % products.Count];
            db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                CompanyId = DemoCompanyId,
                ProductId = prod.Id,
                MovementType = movType,
                Quantity = qty,
                StockBefore = before,
                StockAfter = after,
                Reference = reference,
                CreatedBy = DemoEmail,
                SyncStatus = 0,
                CreatedAt = date,
                IsDeleted = false
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
