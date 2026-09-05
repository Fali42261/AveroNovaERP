using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockExpenseService : IExpenseService
{
    public Task<List<ExpenseModel>> GetAllAsync(Guid companyId)
        => Task.FromResult(MockDataStore.Expenses.Where(e => e.CompanyId == companyId).ToList());

    public Task<ExpenseModel?> GetByIdAsync(Guid id)
        => Task.FromResult(MockDataStore.Expenses.FirstOrDefault(e => e.LocalId == id));

    public Task<(bool Ok, string? Error)> CreateAsync(ExpenseModel expense)
    {
        expense.LocalId    = Guid.NewGuid();
        expense.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Expenses.Add(expense);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> UpdateAsync(ExpenseModel expense)
    {
        var idx = MockDataStore.Expenses.FindIndex(e => e.LocalId == expense.LocalId);
        if (idx < 0) return Task.FromResult((false, "Expense not found."));
        expense.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Expenses[idx] = expense;
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        var item = MockDataStore.Expenses.FirstOrDefault(e => e.LocalId == id);
        if (item == null) return Task.FromResult((false, "Expense not found."));
        MockDataStore.Expenses.Remove(item);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<List<string>> GetCategoriesAsync(Guid companyId) => Task.FromResult(new List<string>
    {
        "Office Supplies", "Travel", "Software", "Hardware", "Marketing",
        "Utilities", "Rent", "Salaries", "Maintenance", "Other"
    });
}
