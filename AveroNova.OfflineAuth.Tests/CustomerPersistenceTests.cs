using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class CustomerPersistenceTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private IDbContextFactory<LocalAppDbContext> _factory = null!;
    private LocalCustomerService _service = null!;
    private readonly Guid _companyId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"swapdigit-customers-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LocalAppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _factory = new Factory(options);

        await using (var db = await _factory.CreateDbContextAsync())
            await db.Database.EnsureCreatedAsync();

        var session = new AppSessionContext();
        session.SetFromLocal(
            new LocalUserEntity { Id = Guid.NewGuid(), FullName = "Customer User", Email = "customer@test.local" },
            new LocalCompanyEntity { Id = _companyId, CompanyName = "Customer Company" },
            ["Company.Owner"],
            ["Customers.Manage"],
            Guid.NewGuid());
        _service = new LocalCustomerService(_factory, session);
    }

    public Task DisposeAsync()
    {
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CustomerCrud_PersistsAndQueuesEveryOperation()
    {
        var customer = new CustomerModel
        {
            CompanyId = _companyId,
            Name = "Test Customer",
            Email = "first@test.local",
            Phone = "9999999999"
        };

        var created = await _service.CreateAsync(customer);
        Assert.True(created.Ok, created.Error);
        Assert.NotEqual(Guid.Empty, customer.LocalId);

        var saved = await _service.GetByIdAsync(customer.LocalId);
        Assert.NotNull(saved);
        Assert.Equal("Test Customer", saved.Name);

        saved.Name = "Edited Customer";
        saved.Email = "edited@test.local";
        var updated = await _service.UpdateAsync(saved);
        Assert.True(updated.Ok, updated.Error);
        Assert.Equal("Edited Customer", (await _service.GetByIdAsync(saved.LocalId))?.Name);

        var deleted = await _service.DeleteAsync(saved.LocalId);
        Assert.True(deleted.Ok, deleted.Error);
        Assert.Null(await _service.GetByIdAsync(saved.LocalId));

        await using var db = await _factory.CreateDbContextAsync();
        Assert.Empty(await db.Customers.ToListAsync());
        Assert.Equal(3, await db.SyncQueue.CountAsync());
    }

    [Fact]
    public async Task CustomerCrud_RejectsAnotherCompany()
    {
        var result = await _service.CreateAsync(new CustomerModel
        {
            CompanyId = Guid.NewGuid(),
            Name = "Wrong Company"
        });

        Assert.False(result.Ok);
        Assert.Contains("access", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await _service.GetAllAsync(Guid.NewGuid()));
    }

    private sealed class Factory(DbContextOptions<LocalAppDbContext> options)
        : IDbContextFactory<LocalAppDbContext>
    {
        public LocalAppDbContext CreateDbContext() => new(options);
        public Task<LocalAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
