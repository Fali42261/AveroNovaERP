using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Entities;

public sealed class Invoice : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public string ItemsJson { get; set; } = "[]";
    public decimal DiscountPct { get; set; }
    public decimal TaxPct { get; set; }
    public int PaymentMethod { get; set; }
    public string Notes { get; set; } = string.Empty;
    public int Status { get; set; }
    public decimal PaidAmount { get; set; }

    public void ApplyUpdate(string invoiceNumber, Guid customerId, string customerName, DateTime invoiceDate,
        DateTime dueDate, string itemsJson, decimal discountPct, decimal taxPct, int paymentMethod,
        string? notes, int status, decimal paidAmount)
    {
        InvoiceNumber = invoiceNumber.Trim();
        CustomerId = customerId;
        CustomerName = customerName.Trim();
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        ItemsJson = string.IsNullOrWhiteSpace(itemsJson) ? "[]" : itemsJson;
        DiscountPct = discountPct;
        TaxPct = taxPct;
        PaymentMethod = paymentMethod;
        Notes = notes?.Trim() ?? string.Empty;
        Status = status;
        PaidAmount = paidAmount;
        MarkPendingChange();
    }
}
