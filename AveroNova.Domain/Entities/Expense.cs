using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Entities;

public sealed class Expense : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public int Method { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int Status { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;

    public void ApplyUpdate(string category, string? description, decimal amount, DateTime expenseDate,
        int method, string? reference, string? notes, int status, string? approvedBy)
    {
        Category = category.Trim();
        Description = description?.Trim() ?? string.Empty;
        Amount = amount;
        ExpenseDate = expenseDate;
        Method = method;
        Reference = reference?.Trim() ?? string.Empty;
        Notes = notes?.Trim() ?? string.Empty;
        Status = status;
        ApprovedBy = approvedBy?.Trim() ?? string.Empty;
        MarkPendingChange();
    }
}
