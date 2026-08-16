namespace AveroNova.App.UI.Models;

// ═══════════════════════════════════════════════════════════════
//  AVERONOVA ERP — INVENTORY MODELS
// ═══════════════════════════════════════════════════════════════

public class InventoryItemModel : BaseModel
{
    public Guid    ProductId       { get; set; }
    public string  ProductName     { get; set; } = string.Empty;
    public string  SKU             { get; set; } = string.Empty;
    public string  Category        { get; set; } = string.Empty;
    public int     CurrentStock    { get; set; }
    public int     AvailableStock  { get; set; }
    public int     ReservedStock   { get; set; }
    public int     MinimumStock    { get; set; }
    public DateTime LastUpdated    { get; set; } = DateTime.UtcNow;
    public Guid    CompanyId       { get; set; }

    public bool   IsLowStock       => CurrentStock <= MinimumStock;
    public string StockStatusLabel => IsLowStock ? "Low Stock" : "In Stock";
}

public class StockMovementModel : BaseModel
{
    public Guid               ProductId     { get; set; }
    public string             ProductName   { get; set; } = string.Empty;
    public string             SKU           { get; set; } = string.Empty;
    public StockMovementType  Type          { get; set; }
    public int                Quantity      { get; set; }
    public int                StockBefore   { get; set; }
    public int                StockAfter    { get; set; }
    public string             Reference     { get; set; } = string.Empty;
    public string             Notes         { get; set; } = string.Empty;
    public string             CreatedBy     { get; set; } = string.Empty;
    public Guid               CompanyId     { get; set; }

    public string TypeLabel => Type switch
    {
        StockMovementType.In         => "Stock In",
        StockMovementType.Out        => "Stock Out",
        StockMovementType.Adjustment => "Adjustment",
        StockMovementType.Transfer   => "Transfer",
        StockMovementType.Return     => "Return",
        _                            => "Unknown"
    };
}

public class StockAdjustmentModel : BaseModel
{
    public Guid     ProductId    { get; set; }
    public string   ProductName  { get; set; } = string.Empty;
    public int      CurrentStock { get; set; }
    public int      NewStock     { get; set; }
    public string   Reason       { get; set; } = string.Empty;
    public string   Notes        { get; set; } = string.Empty;
    public string   AdjustedBy   { get; set; } = string.Empty;
    public Guid     CompanyId    { get; set; }

    public int Difference => NewStock - CurrentStock;
}
