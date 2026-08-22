namespace AveroNova.Domain.Entities;

public class Purchase : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string PurchaseNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public DateTime DueDate { get; set; }
    public int PaymentMethod { get; set; }
    public decimal PaidAmount { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int Status { get; set; }
    public int SyncStatus { get; set; }
    public List<PurchaseItem> Items { get; set; } = [];
}

public class PurchaseItem : BaseEntity
{
    public Guid PurchaseId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal TaxPct { get; set; }
    public Purchase Purchase { get; set; } = null!;
}
