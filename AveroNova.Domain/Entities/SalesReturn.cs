using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Entities;

public sealed class SalesReturn : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public string ItemsJson { get; set; } = "[]";
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
    public int Status { get; set; }

    public void ApplyUpdate(SalesReturn source)
    {
        InvoiceId = source.InvoiceId;
        InvoiceNumber = source.InvoiceNumber;
        CustomerId = source.CustomerId;
        CustomerName = source.CustomerName;
        ReturnDate = source.ReturnDate;
        ItemsJson = string.IsNullOrWhiteSpace(source.ItemsJson) ? "[]" : source.ItemsJson;
        Reason = source.Reason.Trim();
        Notes = source.Notes.Trim();
        RefundAmount = source.RefundAmount;
        Status = source.Status;
        MarkPendingChange();
    }
}
