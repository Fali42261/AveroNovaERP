using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class OfflineBusinessCrudTests : IAsyncLifetime
{
    private readonly Guid _companyId = Guid.NewGuid();
    private string _dbPath = null!;
    private IDbContextFactory<LocalAppDbContext> _factory = null!;
    private AppSessionContext _session = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"swapdigit-offline-crud-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LocalAppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _factory = new Factory(options);

        await using (var db = await _factory.CreateDbContextAsync())
            await db.Database.EnsureCreatedAsync();

        _session = new AppSessionContext();
        _session.SetFromLocal(
            new LocalUserEntity { Id = Guid.NewGuid(), FullName = "Offline Owner", Email = "owner@offline.local", IsActive = true },
            new LocalCompanyEntity { Id = _companyId, CompanyName = "Offline Company", IsActive = true },
            ["Company Owner"],
            OfflineRegistrationStore.OwnerPermissions,
            Guid.NewGuid());
    }

    public Task DisposeAsync()
    {
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Company_InsertGetUpdateDelete_PersistsAllOfflineFields()
    {
        var userId = _session.CurrentUserId!.Value;
        await using (var db = await _factory.CreateDbContextAsync())
        {
            db.Users.Add(new LocalUserEntity { Id = userId, FullName = "Offline Owner", Email = "owner@offline.local", IsActive = true });
            db.Companies.Add(new LocalCompanyEntity { Id = _companyId, CompanyName = "Current Company", IsActive = true });
            db.UserCompanies.Add(new LocalUserCompanyEntity
            {
                Id = Guid.NewGuid(), UserId = userId, CompanyId = _companyId,
                IsDefault = true, IsOwner = true, IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var sessions = new LocalAuthSessionStore(_factory);
        var installation = new InstallationService(_factory, NullLogger<InstallationService>.Instance);
        await installation.EnsureInitializedAsync();
        var service = new LocalCompanyService(_factory, _session, sessions, installation);
        var model = new CompanyModel
        {
            Name = "Second Company",
            Email = "second@offline.local",
            Phone = "9999999999",
            Address = "Offline Street",
            City = "Delhi",
            Country = "India",
            TaxNumber = "GST-1",
            RegistrationNo = "REG-1",
            Currency = "INR",
            CurrencySymbol = "₹",
            InvoicePrefix = "SD",
            Website = "https://offline.local"
        };

        Assert.True((await service.CreateAsync(model)).Ok);
        var created = await service.GetByIdAsync(model.LocalId);
        Assert.NotNull(created);
        Assert.Equal("Delhi", created!.City);
        Assert.Equal("GST-1", created.TaxNumber);
        Assert.Equal("INR", created.Currency);
        Assert.Equal("SD", created.InvoicePrefix);

        model.City = "New Delhi";
        model.TaxNumber = "GST-2";
        model.InvoicePrefix = "SWD";
        Assert.True((await service.UpdateAsync(model)).Ok);
        var updated = await service.GetByIdAsync(model.LocalId);
        Assert.NotNull(updated);
        Assert.Equal("New Delhi", updated!.City);
        Assert.Equal("GST-2", updated.TaxNumber);
        Assert.Equal("SWD", updated.InvoicePrefix);

        Assert.True((await service.DeleteAsync(model.LocalId)).Ok);
        Assert.Null(await service.GetByIdAsync(model.LocalId));
        await using (var db = await _factory.CreateDbContextAsync())
            Assert.False((await db.Companies.SingleAsync(x => x.Id == model.LocalId)).IsActive);

        await AssertQueueAsync("Company", expectedOperations: 3);
    }

    [Fact]
    public async Task Product_InsertGetUpdateDelete_PersistsOffline()
    {
        var service = new LocalProductService(_factory, _session);
        var model = new ProductModel
        {
            CompanyId = _companyId,
            Name = "Offline Product",
            SKU = "OFF-1",
            SellingPrice = 125m,
            Stock = 10
        };

        Assert.True((await service.CreateAsync(model)).Ok);
        var created = await service.GetByIdAsync(model.LocalId);
        Assert.NotNull(created);
        Assert.Equal("Offline Product", created!.Name);
        Assert.Single(await service.GetAllAsync(_companyId));

        model.Name = "Updated Offline Product";
        model.Stock = 15;
        Assert.True((await service.UpdateAsync(model)).Ok);
        var updated = await service.GetByIdAsync(model.LocalId);
        Assert.NotNull(updated);
        Assert.Equal("Updated Offline Product", updated!.Name);
        Assert.Equal(15, updated.Stock);

        Assert.True((await service.DeleteAsync(model.LocalId)).Ok);
        Assert.Null(await service.GetByIdAsync(model.LocalId));
        Assert.Empty(await service.GetAllAsync(_companyId));

        await AssertQueueAsync("Product", expectedOperations: 3);
    }

    [Fact]
    public async Task Invoice_InsertGetUpdateDelete_PersistsOffline()
    {
        var service = new LocalBillingService(_factory, _session);
        var model = new InvoiceModel
        {
            CompanyId = _companyId,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Offline Customer",
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            Status = InvoiceStatus.Sent,
            Items = [new InvoiceLineItem { ProductId = Guid.NewGuid(), ProductName = "Item", Quantity = 2, UnitPrice = 50m }]
        };

        Assert.True((await service.CreateAsync(model)).Ok);
        Assert.NotEmpty(model.InvoiceNumber);
        var created = await service.GetByIdAsync(model.LocalId);
        Assert.NotNull(created);
        Assert.Equal(100m, created!.GrandTotal);
        Assert.Single(await service.GetAllAsync(_companyId));

        model.Items[0].Quantity = 3;
        model.Notes = "Updated offline";
        Assert.True((await service.UpdateAsync(model)).Ok);
        var updated = await service.GetByIdAsync(model.LocalId);
        Assert.NotNull(updated);
        Assert.Equal(150m, updated!.GrandTotal);
        Assert.Equal("Updated offline", updated.Notes);

        Assert.True((await service.DeleteAsync(model.LocalId)).Ok);
        Assert.Null(await service.GetByIdAsync(model.LocalId));
        Assert.Empty(await service.GetAllAsync(_companyId));

        await AssertQueueAsync("Invoice", expectedOperations: 3);
    }

    [Fact]
    public async Task Payment_InsertGetUpdateDelete_PersistsOffline()
    {
        var service = new LocalPaymentService(_factory, _session);
        var model = new PaymentModel
        {
            CompanyId = _companyId,
            PartyName = "Offline Party",
            Amount = 500m,
            PaymentDate = DateTime.Today,
            Method = PaymentMethod.Cash,
            Status = PaymentStatus.Completed
        };

        Assert.True((await service.CreateAsync(model)).Ok);
        Assert.NotEmpty(model.PaymentNumber);
        var created = await service.GetByIdAsync(model.LocalId);
        Assert.NotNull(created);
        Assert.Equal(500m, created!.Amount);
        Assert.Single(await service.GetAllAsync(_companyId));

        model.Amount = 650m;
        model.Reference = "OFFLINE-UPDATED";
        Assert.True((await service.UpdateAsync(model)).Ok);
        var updated = await service.GetByIdAsync(model.LocalId);
        Assert.NotNull(updated);
        Assert.Equal(650m, updated!.Amount);
        Assert.Equal("OFFLINE-UPDATED", updated.Reference);

        Assert.True((await service.DeleteAsync(model.LocalId)).Ok);
        Assert.Null(await service.GetByIdAsync(model.LocalId));
        Assert.Empty(await service.GetAllAsync(_companyId));

        await AssertQueueAsync("Payment", expectedOperations: 3);
    }

    private async Task AssertQueueAsync(string entityType, int expectedOperations)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var queued = await db.SyncQueue.Where(x => x.EntityType == entityType).ToListAsync();
        Assert.Equal(expectedOperations, queued.Count);
        Assert.All(queued, item => Assert.Equal((int)RecordSyncStatus.Pending, item.Status));
    }

    private sealed class Factory(DbContextOptions<LocalAppDbContext> options)
        : IDbContextFactory<LocalAppDbContext>
    {
        public LocalAppDbContext CreateDbContext() => new(options);
        public Task<LocalAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
