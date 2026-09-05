using AveroNova.App.UI.Data;
using AveroNova.App.UI.Services;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class InventoryPersistenceTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private IDbContextFactory<LocalAppDbContext> _factory = null!;
    private AppSessionContext _session = null!;
    private LocalInventoryService _service = null!;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"averonova-inventory-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LocalAppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _factory = new TestDbContextFactory(options);

        await using (var db = await _factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
            db.Products.Add(new LocalProductEntity
            {
                Id = _productId,
                CompanyId = _companyId,
                Name = "Test Product",
                SKU = "TEST-001",
                Category = "Test",
                Stock = 10,
                MinimumStock = 2,
                Status = 0,
                SyncStatus = (int)RecordSyncStatus.Synced
            });
            await db.SaveChangesAsync();
        }

        _session = new AppSessionContext();
        _session.SetFromLocal(
            new LocalUserEntity { Id = _userId, FullName = "Inventory User", Email = "inventory@test.local" },
            new LocalCompanyEntity { Id = _companyId, CompanyName = "Inventory Company" },
            ["Company.Owner"],
            ["Inventory.View"],
            Guid.NewGuid());
        _service = new LocalInventoryService(_factory, _session);
    }

    public Task DisposeAsync()
    {
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AdjustStock_UpdatesProductAndPersistsMovementAtomically()
    {
        var result = await _service.AdjustStockAsync(new()
        {
            CompanyId = _companyId,
            ProductId = _productId,
            CurrentStock = 10,
            NewStock = 16,
            Reason = "Opening stock correction",
            AdjustedBy = "Inventory User"
        });

        Assert.True(result.Ok, result.Error);
        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(16, (await db.Products.SingleAsync()).Stock);
        var movement = await db.StockMovements.SingleAsync();
        Assert.Equal(6, movement.Quantity);
        Assert.Equal(10, movement.StockBefore);
        Assert.Equal(16, movement.StockAfter);
        Assert.Equal(2, await db.SyncQueue.CountAsync());
    }

    [Fact]
    public async Task InventoryAndMovements_AreFilteredByCurrentCompany()
    {
        Assert.Single(await _service.GetInventoryAsync(_companyId));
        Assert.Empty(await _service.GetInventoryAsync(Guid.NewGuid()));

        var denied = await _service.AdjustStockAsync(new()
        {
            CompanyId = Guid.NewGuid(),
            ProductId = _productId,
            NewStock = 5,
            Reason = "Must be denied"
        });
        Assert.False(denied.Ok);
        Assert.Contains("access", denied.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdjustStock_RejectsNegativeAndUnchangedQuantities()
    {
        var negative = await _service.AdjustStockAsync(new()
        {
            CompanyId = _companyId,
            ProductId = _productId,
            NewStock = -1,
            Reason = "Invalid"
        });
        Assert.False(negative.Ok);

        var unchanged = await _service.AdjustStockAsync(new()
        {
            CompanyId = _companyId,
            ProductId = _productId,
            NewStock = 10,
            Reason = "No change"
        });
        Assert.False(unchanged.Ok);

        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(10, (await db.Products.SingleAsync()).Stock);
        Assert.Empty(await db.StockMovements.ToListAsync());
    }

    private sealed class TestDbContextFactory(DbContextOptions<LocalAppDbContext> options)
        : IDbContextFactory<LocalAppDbContext>
    {
        public LocalAppDbContext CreateDbContext() => new(options);
        public Task<LocalAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
