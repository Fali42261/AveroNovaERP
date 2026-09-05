using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class ReportingCalculationTests : IAsyncLifetime
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _otherCompanyId = Guid.NewGuid();
    private string _path = null!;
    private IDbContextFactory<LocalAppDbContext> _factory = null!;
    private LocalReportingService _service = null!;

    public async Task InitializeAsync()
    {
        _path = Path.Combine(Path.GetTempPath(), $"averonova-reports-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LocalAppDbContext>().UseSqlite($"Data Source={_path}").Options;
        _factory = new Factory(options);
        await using var db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        Seed(db, _companyId, "INV-1", 1000, 200, DateTime.Today);
        Seed(db, _otherCompanyId, "INV-OTHER", 9000, 0, DateTime.Today);
        await db.SaveChangesAsync();

        var session = new AppSessionContext();
        session.SetFromLocal(
            new LocalUserEntity { Id = Guid.NewGuid(), FullName = "Owner", Email = "owner@test.local" },
            new LocalCompanyEntity { Id = _companyId, CompanyName = "Current" },
            ["Company.Owner"], ["Dashboard.View"], Guid.NewGuid());
        _service = new LocalReportingService(_factory, session);
    }

    public Task DisposeAsync()
    {
        try { File.Delete(_path); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Summary_CalculatesFinancialsFromCurrentCompanyOnly()
    {
        var (summary, error) = await _service.GetSummaryAsync(
            _companyId, new ReportPeriod(DateTime.Today.AddDays(-1), DateTime.Today));

        Assert.Null(error);
        Assert.NotNull(summary);
        Assert.Equal(1100m, summary.GrossSales);
        Assert.Equal(100m, summary.SalesReturns);
        Assert.Equal(1000m, summary.NetRevenue);
        Assert.Equal(550m, summary.GrossPurchases);
        Assert.Equal(50m, summary.PurchaseReturns);
        Assert.Equal(500m, summary.NetPurchases);
        Assert.Equal(100m, summary.OperatingExpenses);
        Assert.Equal(400m, summary.NetProfit);
        Assert.Equal(900m, summary.OutstandingReceivables);
        Assert.Equal(500m, summary.OutstandingPayables);
        Assert.Equal(200m, summary.PaymentsReceived);
        Assert.Equal(75m, summary.PaymentsPaid);
        Assert.Equal(1, summary.InvoiceCount);
        Assert.Equal(1, summary.LowStockCount);
    }

    [Fact]
    public async Task Summary_RejectsOtherCompanyAndInvalidPeriod()
    {
        var foreign = await _service.GetSummaryAsync(_otherCompanyId, ReportPeriod.CurrentMonth(DateTime.Today));
        Assert.Null(foreign.Summary);
        Assert.Contains("access", foreign.Error, StringComparison.OrdinalIgnoreCase);

        var invalid = await _service.GetSummaryAsync(
            _companyId, new ReportPeriod(DateTime.Today, DateTime.Today.AddDays(-1)));
        Assert.Null(invalid.Summary);
        Assert.Contains("From date", invalid.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static void Seed(LocalAppDbContext db, Guid companyId, string invoiceNumber, decimal unitPrice, decimal paid, DateTime date)
    {
        var customerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var purchaseId = Guid.NewGuid();
        db.Customers.Add(new LocalCustomerEntity { Id = customerId, CompanyId = companyId, Name = "Customer", Status = 0 });
        db.Products.Add(new LocalProductEntity { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Low", SKU = Guid.NewGuid().ToString("N"), Status = 0, Stock = 2, MinimumStock = 5 });
        db.Suppliers.Add(new LocalSupplierEntity { Id = supplierId, CompanyId = companyId, Name = "Supplier", IsActive = true });
        db.Invoices.Add(new LocalInvoiceEntity
        {
            Id = invoiceId, CompanyId = companyId, InvoiceNumber = invoiceNumber, CustomerId = customerId,
            InvoiceDate = date, DueDate = date.AddDays(-1), Status = (int)InvoiceStatus.Sent, PaidAmount = paid,
            ItemsJson = JsonSerializer.Serialize(new[] { new InvoiceLineItem { ProductId = Guid.NewGuid(), UnitPrice = unitPrice, Quantity = 1, TaxPct = 10 } })
        });
        db.Purchases.Add(new LocalPurchaseEntity
        {
            Id = purchaseId, CompanyId = companyId, PurchaseNumber = "PO-" + invoiceNumber, SupplierId = supplierId,
            PurchaseDate = date, DueDate = date, Status = (int)PurchaseStatus.Received, PaidAmount = 50,
            ItemsJson = JsonSerializer.Serialize(new[] { new PurchaseLineItem { ProductId = Guid.NewGuid(), UnitPrice = unitPrice / 2, Quantity = 1, TaxPct = 10 } })
        });
        db.Expenses.Add(new LocalExpenseEntity { Id = Guid.NewGuid(), CompanyId = companyId, Category = "Rent", Amount = 100, ExpenseDate = date, Status = (int)ExpenseStatus.Paid });
        db.SalesReturns.Add(new LocalSalesReturnEntity { Id = Guid.NewGuid(), CompanyId = companyId, ReturnNumber = "SR-1", InvoiceId = invoiceId, CustomerId = customerId, ReturnDate = date, RefundAmount = 100, Status = (int)ReturnStatus.Completed });
        db.PurchaseReturns.Add(new LocalPurchaseReturnEntity { Id = Guid.NewGuid(), CompanyId = companyId, ReturnNumber = "PR-1", PurchaseId = purchaseId, SupplierId = supplierId, ReturnDate = date, RefundAmount = 50, Status = (int)ReturnStatus.Completed });
        db.Payments.AddRange(
            new LocalPaymentEntity { Id = Guid.NewGuid(), CompanyId = companyId, PaymentNumber = "PAY-IN", PartyId = customerId, Amount = 200, PaymentDate = date, Status = (int)PaymentStatus.Completed },
            new LocalPaymentEntity { Id = Guid.NewGuid(), CompanyId = companyId, PaymentNumber = "PAY-OUT", PartyId = supplierId, IsSupplier = true, Amount = 75, PaymentDate = date, Status = (int)PaymentStatus.Completed });
    }

    private sealed class Factory(DbContextOptions<LocalAppDbContext> options) : IDbContextFactory<LocalAppDbContext>
    {
        public LocalAppDbContext CreateDbContext() => new(options);
    }
}
