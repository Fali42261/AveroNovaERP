namespace AveroNova.App.UI.Models;

// ═══════════════════════════════════════════════════════════════
//  AVERONOVA ERP — PRODUCT MODEL
// ═══════════════════════════════════════════════════════════════

public class ProductModel : BaseModel
{
    public string   Name            { get; set; } = string.Empty;
    public string   SKU             { get; set; } = string.Empty;
    public string   Barcode         { get; set; } = string.Empty;
    public string   Category        { get; set; } = string.Empty;
    public string   Brand           { get; set; } = string.Empty;
    public string   Unit            { get; set; } = "pcs";
    public decimal  PurchasePrice   { get; set; }
    public decimal  SellingPrice    { get; set; }
    public decimal  TaxPercent      { get; set; }
    public int      Stock           { get; set; }
    public int      MinimumStock    { get; set; }
    public string   Description     { get; set; } = string.Empty;
    public string?  ImageUrl        { get; set; }
    public ProductStatus Status     { get; set; } = ProductStatus.Active;
    public Guid     CompanyId       { get; set; }

    public bool  IsLowStock => Stock <= MinimumStock;
    public string StatusLabel => Status switch
    {
        ProductStatus.Active       => "Active",
        ProductStatus.Inactive     => "Inactive",
        ProductStatus.Discontinued => "Discontinued",
        _                          => "Unknown"
    };

    public decimal Margin => SellingPrice > 0
        ? Math.Round((SellingPrice - PurchasePrice) / SellingPrice * 100, 1)
        : 0;
}
