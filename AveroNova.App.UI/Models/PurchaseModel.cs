namespace AveroNova.App.UI.Models;

public class PurchaseLineItem
{
    public Guid    ProductId   { get; set; }
    public string  ProductName { get; set; } = string.Empty;
    public string  SKU         { get; set; } = string.Empty;
    public decimal UnitPrice   { get; set; }
    public int     Quantity    { get; set; }
    public decimal TaxPct      { get; set; }
    public decimal LineTotal   => UnitPrice * Quantity;
    public decimal TaxAmount   => LineTotal * TaxPct / 100;
    public decimal GrandTotal  => LineTotal + TaxAmount;
}

public class PurchaseModel : BaseModel
{
    public string              PurchaseNumber { get; set; } = string.Empty;
    public Guid                SupplierId     { get; set; }
    public string              SupplierName   { get; set; } = string.Empty;
    public DateTime            PurchaseDate   { get; set; } = DateTime.Today;
    public DateTime            DueDate        { get; set; } = DateTime.Today.AddDays(30);
    public List<PurchaseLineItem> Items       { get; set; } = [];
    public PaymentMethod       PaymentMethod  { get; set; } = PaymentMethod.Cash;
    public string              Reference      { get; set; } = string.Empty;
    public string              Notes          { get; set; } = string.Empty;
    public PurchaseStatus      Status         { get; set; } = PurchaseStatus.Draft;
    public Guid                CompanyId      { get; set; }

    public decimal Subtotal   => Items.Sum(i => i.LineTotal);
    public decimal TaxTotal   => Items.Sum(i => i.TaxAmount);
    public decimal GrandTotal => Subtotal + TaxTotal;
    public decimal PaidAmount { get; set; }
    public decimal DueAmount  => GrandTotal - PaidAmount;

    public string StatusLabel => Status switch
    {
        PurchaseStatus.Draft           => "Draft",
        PurchaseStatus.Ordered         => "Ordered",
        PurchaseStatus.PartialReceived => "Partial",
        PurchaseStatus.Received        => "Received",
        PurchaseStatus.Cancelled       => "Cancelled",
        _                              => "Unknown"
    };
}
