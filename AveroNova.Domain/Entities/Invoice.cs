namespace AveroNova.Domain.Entities;

public class Invoice : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal TaxPct { get; set; }
    public int PaymentMethod { get; set; }
    public decimal PaidAmount { get; set; }
    public string Notes { get; set; } = string.Empty;
    public int Status { get; set; }
    public int SyncStatus { get; set; }
    public List<InvoiceItem> Items { get; set; } = [];
}

public class InvoiceItem : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal TaxPct { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
