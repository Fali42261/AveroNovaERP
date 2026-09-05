using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using Xunit;

namespace AveroNova.API.Tests;

public sealed class ExpenseDomainTests
{
    [Fact]
    public void ApplyUpdate_UpdatesExpenseFields()
    {
        var expense = NewExpense();
        expense.ApplyUpdate(" Travel ", " Client visit ", 2500m, new DateTime(2026, 9, 5), 1, " UPI-42 ", " Taxi ", 1, " Ali ");
        Assert.Equal("Travel", expense.Category);
        Assert.Equal("Client visit", expense.Description);
        Assert.Equal(2500m, expense.Amount);
        Assert.Equal(new DateTime(2026, 9, 5), expense.ExpenseDate);
        Assert.Equal(1, expense.Method);
        Assert.Equal("UPI-42", expense.Reference);
        Assert.Equal("Taxi", expense.Notes);
        Assert.Equal(1, expense.Status);
        Assert.Equal("Ali", expense.ApprovedBy);
    }

    [Fact]
    public void ApplyUpdate_MarksPendingAndIncrementsVersion()
    {
        var expense = NewExpense();
        expense.SyncStatus = RecordSyncStatus.Synced;
        expense.SyncVersion = 4;
        expense.ApplyUpdate("Rent", null, 1000m, DateTime.Today, 0, null, null, 0, null);
        Assert.Equal(RecordSyncStatus.Pending, expense.SyncStatus);
        Assert.Equal(5, expense.SyncVersion);
    }

    [Fact]
    public void ApplyUpdate_NormalizesNullableStrings()
    {
        var expense = NewExpense();
        expense.ApplyUpdate("Other", null, 10m, DateTime.Today, 0, null, null, 0, null);
        Assert.Equal(string.Empty, expense.Description);
        Assert.Equal(string.Empty, expense.Reference);
        Assert.Equal(string.Empty, expense.Notes);
        Assert.Equal(string.Empty, expense.ApprovedBy);
    }

    private static Expense NewExpense() => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = Guid.NewGuid(),
        Category = "Office Supplies",
        Amount = 100m,
        ExpenseDate = DateTime.Today
    };
}
