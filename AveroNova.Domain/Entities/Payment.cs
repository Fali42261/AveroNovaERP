namespace AveroNova.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Guid? SupplierId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public int PaymentMethod { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int SyncStatus { get; set; }
}
