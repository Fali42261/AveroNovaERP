using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

public interface IExpenseService
{
    Task<List<ExpenseModel>> GetAllAsync(Guid companyId);
    Task<ExpenseModel?>      GetByIdAsync(Guid id);
    Task<(bool Ok, string? Error)> CreateAsync(ExpenseModel expense);
    Task<(bool Ok, string? Error)> UpdateAsync(ExpenseModel expense);
    Task<(bool Ok, string? Error)> DeleteAsync(Guid id);
    Task<List<string>>             GetCategoriesAsync(Guid companyId);
}
