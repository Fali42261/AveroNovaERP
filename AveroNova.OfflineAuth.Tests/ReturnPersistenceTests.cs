using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class ReturnPersistenceTests : IAsyncLifetime
{
    private string _path = null!;
    private IDbContextFactory<LocalAppDbContext> _factory = null!;
    private LocalReturnService _service = null!;
    private readonly Guid _company = Guid.NewGuid();
    private readonly Guid _invoice = Guid.NewGuid();
    private readonly Guid _purchase = Guid.NewGuid();
    private readonly Guid _salesProduct = Guid.NewGuid();
    private readonly Guid _purchaseProduct = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _path = Path.Combine(Path.GetTempPath(), $"averonova-returns-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LocalAppDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options;
        _factory = new Factory(options);

        await using (var db = await _factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
            db.Products.AddRange(
                new LocalProductEntity
                {
                    Id = _salesProduct,
                    CompanyId = _company,
                    Name = "Sales Item",
                    SKU = "RET-SALE",
                    Category = "Returns",
                    Stock = 10,
                    MinimumStock = 0,
                    Status = 0,
                    SyncStatus = (int)RecordSyncStatus.Synced
                },
                new LocalProductEntity
                {
                    Id = _purchaseProduct,
                    CompanyId = _company,
                    Name = "Purchase Part",
                    SKU = "RET-PURCHASE",
                    Category = "Returns",
                    Stock = 5,
                    MinimumStock = 0,
                    Status = 0,
                    SyncStatus = (int)RecordSyncStatus.Synced
                });

            db.Invoices.Add(new LocalInvoiceEntity
            {
                Id = _invoice,
                CompanyId = _company,
                InvoiceNumber = "INV-1",
                CustomerId = Guid.NewGuid(),
                CustomerName = "Customer",
                ItemsJson = JsonSerializer.Serialize(new[]
                {
                    new InvoiceLineItem { ProductId = _salesProduct, ProductName = "Sales Item", Quantity = 2, UnitPrice = 50 }
                })
            });
            db.Purchases.Add(new LocalPurchaseEntity
            {
                Id = _purchase,
                CompanyId = _company,
                PurchaseNumber = "PO-1",
                SupplierId = Guid.NewGuid(),
                SupplierName = "Supplier",
                ItemsJson = JsonSerializer.Serialize(new[]
                {
                    new PurchaseLineItem { ProductId = _purchaseProduct, ProductName = "Purchase Part", Quantity = 2, UnitPrice = 40 }
                })
            });
            await db.SaveChangesAsync();
        }

        var session = new AppSessionContext();
        session.SetFromLocal(
            new LocalUserEntity { Id = Guid.NewGuid(), Email = "r@test.local", FullName = "Returns" },
            new LocalCompanyEntity { Id = _company, CompanyName = "Returns Co" },
            ["Owner"],
            ["Returns.Manage"],
            Guid.NewGuid());
        _service = new LocalReturnService(_factory, session, new OfflineConnectivity(), new NoopReturnSync());
    }

    public Task DisposeAsync()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PendingSalesReturn_PersistsQueueWithoutChangingStock()
    {
        var sr = SalesReturn(1, 50, ReturnStatus.Pending);
        var result = await _service.CreateSalesReturnAsync(sr);
        Assert.True(result.Ok, result.Error);

        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(10, (await db.Products.SingleAsync(x => x.Id == _salesProduct)).Stock);
        Assert.Single(await db.SalesReturns.ToListAsync());
        Assert.Single(await db.SyncQueue.ToListAsync());
        Assert.Empty(await db.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task CompletedSalesReturn_IncreasesStockAndPersistsMovement()
    {
        var sr = SalesReturn(1, 50, ReturnStatus.Completed);
        Assert.True((await _service.CreateSalesReturnAsync(sr)).Ok);

        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(11, (await db.Products.SingleAsync(x => x.Id == _salesProduct)).Stock);
        var movement = await db.StockMovements.SingleAsync();
        Assert.Equal(1, movement.Quantity);
        Assert.Equal(10, movement.StockBefore);
        Assert.Equal(11, movement.StockAfter);
        Assert.StartsWith("RETURNQ:", movement.Reference);
    }

    [Fact]
    public async Task SalesReturn_StatusTransitions_ApplyAndReverseStockExactlyOnce()
    {
        var sr = SalesReturn(1, 50, ReturnStatus.Pending);
        Assert.True((await _service.CreateSalesReturnAsync(sr)).Ok);

        sr.Status = ReturnStatus.Completed;
        Assert.True((await _service.UpdateSalesReturnAsync(sr)).Ok);
        await using (var db = await _factory.CreateDbContextAsync())
            Assert.Equal(11, (await db.Products.SingleAsync(x => x.Id == _salesProduct)).Stock);

        sr.Status = ReturnStatus.Rejected;
        Assert.True((await _service.UpdateSalesReturnAsync(sr)).Ok);
        await using (var db = await _factory.CreateDbContextAsync())
            Assert.Equal(10, (await db.Products.SingleAsync(x => x.Id == _salesProduct)).Stock);
    }

    [Fact]
    public async Task SalesReturn_DuplicateLinesCannotBypassSourceQuantity()
    {
        var sr = SalesReturn(1, 100, ReturnStatus.Pending);
        sr.Items =
        [
            new ReturnLineItem { ProductId = _salesProduct, ProductName = "Sales Item", Quantity = 1, UnitPrice = 50 },
            new ReturnLineItem { ProductId = _salesProduct, ProductName = "Sales Item", Quantity = 2, UnitPrice = 50 }
        ];
        var result = await _service.CreateSalesReturnAsync(sr);
        Assert.False(result.Ok);

        await using var db = await _factory.CreateDbContextAsync();
        Assert.Empty(await db.SalesReturns.ToListAsync());
        Assert.Empty(await db.SyncQueue.ToListAsync());
    }

    [Fact]
    public async Task SalesReturn_CumulativeAllocationCannotExceedInvoice()
    {
        Assert.True((await _service.CreateSalesReturnAsync(SalesReturn(1, 50, ReturnStatus.Pending))).Ok);
        Assert.True((await _service.CreateSalesReturnAsync(SalesReturn(1, 50, ReturnStatus.Approved))).Ok);
        var third = await _service.CreateSalesReturnAsync(SalesReturn(1, 1, ReturnStatus.Pending));
        Assert.False(third.Ok);
    }

    [Fact]
    public async Task CompletedPurchaseReturn_DecreasesStockAndRejectsInsufficientStockAtomically()
    {
        var pr = PurchaseReturn(2, 80, ReturnStatus.Completed);
        Assert.True((await _service.CreatePurchaseReturnAsync(pr)).Ok);

        await using (var db = await _factory.CreateDbContextAsync())
            Assert.Equal(3, (await db.Products.SingleAsync(x => x.Id == _purchaseProduct)).Stock);

        // A separate source permits a larger return request, but local inventory cannot go negative.
        var secondPurchaseId = Guid.NewGuid();
        await using (var db = await _factory.CreateDbContextAsync())
        {
            db.Purchases.Add(new LocalPurchaseEntity
            {
                Id = secondPurchaseId,
                CompanyId = _company,
                PurchaseNumber = "PO-2",
                SupplierId = Guid.NewGuid(),
                SupplierName = "Supplier",
                ItemsJson = JsonSerializer.Serialize(new[]
                {
                    new PurchaseLineItem { ProductId = _purchaseProduct, ProductName = "Purchase Part", Quantity = 4, UnitPrice = 40 }
                })
            });
            await db.SaveChangesAsync();
        }

        var fail = new PurchaseReturnModel
        {
            CompanyId = _company,
            PurchaseId = secondPurchaseId,
            ReturnDate = DateTime.Today,
            Reason = "Return",
            RefundAmount = 160,
            Status = ReturnStatus.Completed,
            Items = [new ReturnLineItem { ProductId = _purchaseProduct, ProductName = "Purchase Part", Quantity = 4, UnitPrice = 40 }]
        };
        var result = await _service.CreatePurchaseReturnAsync(fail);
        Assert.False(result.Ok);

        await using (var db = await _factory.CreateDbContextAsync())
        {
            Assert.Equal(3, (await db.Products.SingleAsync(x => x.Id == _purchaseProduct)).Stock);
            Assert.Single(await db.PurchaseReturns.ToListAsync());
        }
    }

    private SalesReturnModel SalesReturn(int quantity, decimal refund, ReturnStatus status) => new()
    {
        CompanyId = _company,
        InvoiceId = _invoice,
        ReturnDate = DateTime.Today,
        Reason = "Defective",
        RefundAmount = refund,
        Status = status,
        Items = [new ReturnLineItem { ProductId = _salesProduct, ProductName = "Sales Item", Quantity = quantity, UnitPrice = 50 }]
    };

    private PurchaseReturnModel PurchaseReturn(int quantity, decimal refund, ReturnStatus status) => new()
    {
        CompanyId = _company,
        PurchaseId = _purchase,
        ReturnDate = DateTime.Today,
        Reason = "Wrong item",
        RefundAmount = refund,
        Status = status,
        Items = [new ReturnLineItem { ProductId = _purchaseProduct, ProductName = "Purchase Part", Quantity = quantity, UnitPrice = 40 }]
    };

    private sealed class OfflineConnectivity : IConnectivityService
    {
        public ConnectivityStatus Status { get; private set; } = ConnectivityStatus.Offline;
        public bool IsOnline => false;
        public int PendingCount { get; private set; }
        public event EventHandler? StatusChanged;
        public void UpdateStatus(ConnectivityStatus status) { Status = status; StatusChanged?.Invoke(this, EventArgs.Empty); }
        public void IncrementPending() => PendingCount++;
        public void DecrementPending() { if (PendingCount > 0) PendingCount--; }
    }

    private sealed class NoopReturnSync : IReturnSyncService
    {
        public Task<int> SyncPendingAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class Factory(DbContextOptions<LocalAppDbContext> options) : IDbContextFactory<LocalAppDbContext>
    {
        public LocalAppDbContext CreateDbContext() => new(options);
        public Task<LocalAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
