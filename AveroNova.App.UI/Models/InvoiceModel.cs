namespace AveroNova.App.UI.Models;

// ═══════════════════════════════════════════════════════════════
//  AVERONOVA ERP — INVOICE / BILLING MODELS
// ═══════════════════════════════════════════════════════════════

public class InvoiceLineItem
{
    public Guid    ProductId    { get; set; }
    public string  ProductName  { get; set; } = string.Empty;
    public string  SKU          { get; set; } = string.Empty;
    public decimal UnitPrice    { get; set; }
    public int     Quantity     { get; set; }
    public decimal DiscountPct  { get; set; }
    public decimal TaxPct       { get; set; }
    public decimal LineTotal    => UnitPrice * Quantity * (1 - DiscountPct / 100);
    public decimal TaxAmount    => LineTotal * TaxPct / 100;
    public decimal GrandTotal   => LineTotal + TaxAmount;
}

public class InvoiceModel : BaseModel
{
    public string            InvoiceNumber { get; set; } = string.Empty;
    public Guid              CustomerId    { get; set; }
    public string            CustomerName  { get; set; } = string.Empty;
    public DateTime          InvoiceDate   { get; set; } = DateTime.Today;
    public DateTime          DueDate       { get; set; } = DateTime.Today.AddDays(30);
    public List<InvoiceLineItem> Items     { get; set; } = [];
    public decimal           DiscountPct   { get; set; }
    public decimal           TaxPct        { get; set; }
    public PaymentMethod     PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string            Notes         { get; set; } = string.Empty;
    public InvoiceStatus     Status        { get; set; } = InvoiceStatus.Draft;
    public Guid              CompanyId     { get; set; }

    public decimal Subtotal    => Items.Sum(i => i.LineTotal);
    public decimal TaxTotal    => Items.Sum(i => i.TaxAmount) + Subtotal * TaxPct / 100;
    public decimal GrandTotal  => Subtotal + TaxTotal - DiscountAmount;
    public decimal DiscountAmount => Subtotal * DiscountPct / 100;
    public decimal PaidAmount  { get; set; }
    public decimal DueAmount   => GrandTotal - PaidAmount;

    public string StatusLabel => Status switch
    {
        InvoiceStatus.Draft       => "Draft",
        InvoiceStatus.Sent        => "Sent",
        InvoiceStatus.PartialPaid => "Partial",
        InvoiceStatus.Paid        => "Paid",
        InvoiceStatus.Overdue     => "Overdue",
        InvoiceStatus.Cancelled   => "Cancelled",
        _                         => "Unknown"
    };
}
