using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Entities;

public sealed class Expense : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string ExpenseNumber { get; set; } = string.Empty;
    public DateTime ExpenseDate { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Payee { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int PaymentMethod { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public void ApplyUpdate(string expenseNumber, DateTime expenseDate, string category, string payee,
        decimal amount, int paymentMethod, string? reference, string? notes)
    {
        ExpenseNumber = expenseNumber.Trim();
        ExpenseDate = expenseDate;
        Category = category.Trim();
        Payee = payee.Trim();
        Amount = amount;
        PaymentMethod = paymentMethod;
        Reference = reference?.Trim() ?? string.Empty;
        Notes = notes?.Trim() ?? string.Empty;
        MarkPendingChange();
    }
}
