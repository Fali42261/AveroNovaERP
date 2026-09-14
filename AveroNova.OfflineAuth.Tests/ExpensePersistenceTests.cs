using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class ExpensePersistenceTests : IAsyncLifetime
{
    private readonly Guid _companyId = Guid.NewGuid();
    private string _path = null!;
    private IDbContextFactory<LocalAppDbContext> _factory = null!;
    private LocalExpenseService _service = null!;

    public async Task InitializeAsync()
    {
        _path = Path.Combine(Path.GetTempPath(), $"averonova-expense-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LocalAppDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options;
        _factory = new Factory(options);
        await using (var db = await _factory.CreateDbContextAsync())
            await db.Database.EnsureCreatedAsync();

        var session = new AppSessionContext();
        session.SetFromLocal(
            new LocalUserEntity { Id = Guid.NewGuid(), FullName = "Owner", Email = "owner@test.local" },
            new LocalCompanyEntity { Id = _companyId, CompanyName = "Current" },
            ["Company Owner"],
            ["Expenses.Create", "Expenses.Edit", "Expenses.Delete"],
            Guid.NewGuid());
        _service = new LocalExpenseService(_factory, session);
    }

    public Task DisposeAsync()
    {
        try { File.Delete(_path); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Expense_CreateUpdateDelete_PersistsOfflineAndQueuesEveryChange()
    {
        var expense = new ExpenseModel
        {
            CompanyId = _companyId,
            Category = "Software",
            Description = "Monthly subscription",
            Amount = 499m,
            ExpenseDate = DateTime.Today,
            Method = PaymentMethod.Online,
            Status = ExpenseStatus.Pending
        };

        var created = await _service.CreateAsync(expense);
        Assert.True(created.Ok, created.Error);
        Assert.NotEqual(Guid.Empty, expense.LocalId);

        expense.Amount = 599m;
        expense.Status = ExpenseStatus.Paid;
        expense.ApprovedBy = "Owner";
        var updated = await _service.UpdateAsync(expense);
        Assert.True(updated.Ok, updated.Error);

        var saved = await _service.GetByIdAsync(expense.LocalId);
        Assert.NotNull(saved);
        Assert.Equal(599m, saved!.Amount);
        Assert.Equal(ExpenseStatus.Paid, saved.Status);
        Assert.Equal(SyncStatus.PendingSync, saved.SyncStatus);

        var deleted = await _service.DeleteAsync(expense.LocalId);
        Assert.True(deleted.Ok, deleted.Error);
        Assert.Null(await _service.GetByIdAsync(expense.LocalId));

        await using var db = await _factory.CreateDbContextAsync();
        var queue = await db.SyncQueue.Where(q => q.EntityType == "Expense").OrderBy(q => q.CreatedAt).ToListAsync();
        Assert.Equal(3, queue.Count);
        Assert.Equal(
            new[] { SyncOperation.Create, SyncOperation.Update, SyncOperation.Delete },
            queue.Select(q => (SyncOperation)q.Operation).ToArray());
        Assert.All(queue, q => Assert.Equal((int)RecordSyncStatus.Pending, q.Status));
    }

    [Fact]
    public async Task Expense_InvalidOrForeignData_IsRejectedWithoutQueueing()
    {
        Assert.False((await _service.CreateAsync(new ExpenseModel
        {
            CompanyId = Guid.NewGuid(),
            Category = "Travel",
            Amount = 100m
        })).Ok);
        Assert.False((await _service.CreateAsync(new ExpenseModel
        {
            CompanyId = _companyId,
            Category = "Travel",
            Amount = 0m
        })).Ok);

        await using var db = await _factory.CreateDbContextAsync();
        Assert.Empty(await db.Expenses.ToListAsync());
        Assert.Empty(await db.SyncQueue.Where(q => q.EntityType == "Expense").ToListAsync());
    }

    private sealed class Factory(DbContextOptions<LocalAppDbContext> options) : IDbContextFactory<LocalAppDbContext>
    {
        public LocalAppDbContext CreateDbContext() => new(options);
    }
}
