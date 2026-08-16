namespace AveroNova.App.UI.Models;

public class PaymentModel : BaseModel
{
    public string         PaymentNumber  { get; set; } = string.Empty;
    public Guid           PartyId        { get; set; }  // Customer or Supplier
    public string         PartyName      { get; set; } = string.Empty;
    public bool           IsSupplier     { get; set; }
    public Guid?          InvoiceId      { get; set; }
    public string         InvoiceNumber  { get; set; } = string.Empty;
    public decimal        Amount         { get; set; }
    public PaymentMethod  Method         { get; set; } = PaymentMethod.Cash;
    public DateTime       PaymentDate    { get; set; } = DateTime.Today;
    public string         Reference      { get; set; } = string.Empty;
    public string         Notes          { get; set; } = string.Empty;
    public PaymentStatus  Status         { get; set; } = PaymentStatus.Completed;
    public Guid           CompanyId      { get; set; }

    public string MethodLabel => Method switch
    {
        PaymentMethod.Cash         => "Cash",
        PaymentMethod.BankTransfer => "Bank Transfer",
        PaymentMethod.CreditCard   => "Credit Card",
        PaymentMethod.DebitCard    => "Debit Card",
        PaymentMethod.Cheque       => "Cheque",
        PaymentMethod.Online       => "Online",
        _                          => "Other"
    };

    public string StatusLabel => Status switch
    {
        PaymentStatus.Pending   => "Pending",
        PaymentStatus.Completed => "Completed",
        PaymentStatus.Failed    => "Failed",
        PaymentStatus.Refunded  => "Refunded",
        PaymentStatus.Cancelled => "Cancelled",
        _                       => "Unknown"
    };
}
