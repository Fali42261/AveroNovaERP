namespace AveroNova.Domain.Entities;

public sealed class Product : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Unit { get; set; } = "pcs";
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal TaxPercent { get; set; }
    public int Stock { get; set; }
    public int MinimumStock { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Status { get; set; }

    public void ApplyUpdate(string name, string? sku, string? barcode, string? category, string? brand,
        string? unit, decimal purchasePrice, decimal sellingPrice, decimal taxPercent, int stock,
        int minimumStock, string? description, int status)
    {
        Name = name.Trim();
        SKU = sku?.Trim() ?? string.Empty;
        Barcode = barcode?.Trim() ?? string.Empty;
        Category = category?.Trim() ?? string.Empty;
        Brand = brand?.Trim() ?? string.Empty;
        Unit = string.IsNullOrWhiteSpace(unit) ? "pcs" : unit.Trim();
        PurchasePrice = purchasePrice;
        SellingPrice = sellingPrice;
        TaxPercent = taxPercent;
        Stock = stock;
        MinimumStock = minimumStock;
        Description = description?.Trim() ?? string.Empty;
        Status = status;
        MarkPendingChange();
    }
}
