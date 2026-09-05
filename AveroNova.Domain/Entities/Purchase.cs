using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Entities;

public sealed class Purchase : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string PurchaseNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public DateTime DueDate { get; set; }
    public string ItemsJson { get; set; } = "[]";
    public int PaymentMethod { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int Status { get; set; }
    public decimal PaidAmount { get; set; }

    public void ApplyUpdate(string purchaseNumber, Guid supplierId, string supplierName, DateTime purchaseDate,
        DateTime dueDate, string itemsJson, int paymentMethod, string? reference, string? notes, int status,
        decimal paidAmount)
    {
        PurchaseNumber = purchaseNumber.Trim();
        SupplierId = supplierId;
        SupplierName = supplierName.Trim();
        PurchaseDate = purchaseDate;
        DueDate = dueDate;
        ItemsJson = string.IsNullOrWhiteSpace(itemsJson) ? "[]" : itemsJson;
        PaymentMethod = paymentMethod;
        Reference = reference?.Trim() ?? string.Empty;
        Notes = notes?.Trim() ?? string.Empty;
        Status = status;
        PaidAmount = paidAmount;
        MarkPendingChange();
    }
}
