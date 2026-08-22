namespace AveroNova.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public Guid PartyId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public bool IsSupplier { get; set; }
    public Guid? InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public int Method { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int Status { get; set; }
    public int SyncStatus { get; set; }
}
