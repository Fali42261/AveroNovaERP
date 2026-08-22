namespace AveroNova.Domain.Entities;

public class StockMovement : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Guid ProductId { get; set; }
    public int MovementType { get; set; }
    public int Quantity { get; set; }
    public int StockBefore { get; set; }
    public int StockAfter { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public int SyncStatus { get; set; }

    public Company Company { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
