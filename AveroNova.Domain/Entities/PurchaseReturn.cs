using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Entities;

public sealed class PurchaseReturn : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public Guid PurchaseId { get; set; }
    public string PurchaseNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public string ItemsJson { get; set; } = "[]";
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
    public int Status { get; set; }

    public void ApplyUpdate(PurchaseReturn source)
    {
        PurchaseId = source.PurchaseId;
        PurchaseNumber = source.PurchaseNumber;
        SupplierId = source.SupplierId;
        SupplierName = source.SupplierName;
        ReturnDate = source.ReturnDate;
        ItemsJson = string.IsNullOrWhiteSpace(source.ItemsJson) ? "[]" : source.ItemsJson;
        Reason = source.Reason.Trim();
        Notes = source.Notes.Trim();
        RefundAmount = source.RefundAmount;
        Status = source.Status;
        MarkPendingChange();
    }
}
