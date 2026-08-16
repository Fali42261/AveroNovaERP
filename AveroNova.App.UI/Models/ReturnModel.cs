namespace AveroNova.App.UI.Models;

public class ReturnLineItem
{
    public Guid   ProductId   { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int    Quantity    { get; set; }
    public decimal UnitPrice  { get; set; }
    public decimal Total      => Quantity * UnitPrice;
}

public class SalesReturnModel : BaseModel
{
    public string             ReturnNumber  { get; set; } = string.Empty;
    public Guid               InvoiceId     { get; set; }
    public string             InvoiceNumber { get; set; } = string.Empty;
    public Guid               CustomerId    { get; set; }
    public string             CustomerName  { get; set; } = string.Empty;
    public DateTime           ReturnDate    { get; set; } = DateTime.Today;
    public List<ReturnLineItem> Items       { get; set; } = [];
    public string             Reason        { get; set; } = string.Empty;
    public string             Notes         { get; set; } = string.Empty;
    public decimal            RefundAmount  { get; set; }
    public ReturnStatus       Status        { get; set; } = ReturnStatus.Pending;
    public Guid               CompanyId     { get; set; }

    public decimal TotalAmount => Items.Sum(i => i.Total);
    public string StatusLabel => Status.ToString();
}

public class PurchaseReturnModel : BaseModel
{
    public string             ReturnNumber  { get; set; } = string.Empty;
    public Guid               PurchaseId    { get; set; }
    public string             PurchaseNumber { get; set; } = string.Empty;
    public Guid               SupplierId    { get; set; }
    public string             SupplierName  { get; set; } = string.Empty;
    public DateTime           ReturnDate    { get; set; } = DateTime.Today;
    public List<ReturnLineItem> Items       { get; set; } = [];
    public string             Reason        { get; set; } = string.Empty;
    public string             Notes         { get; set; } = string.Empty;
    public decimal            RefundAmount  { get; set; }
    public ReturnStatus       Status        { get; set; } = ReturnStatus.Pending;
    public Guid               CompanyId     { get; set; }

    public decimal TotalAmount => Items.Sum(i => i.Total);
    public string StatusLabel => Status.ToString();
}
