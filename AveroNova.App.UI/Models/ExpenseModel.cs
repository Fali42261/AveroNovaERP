namespace AveroNova.App.UI.Models;

public class ExpenseModel : BaseModel
{
    public string         Category       { get; set; } = string.Empty;
    public string         Description    { get; set; } = string.Empty;
    public decimal        Amount         { get; set; }
    public DateTime       ExpenseDate    { get; set; } = DateTime.Today;
    public PaymentMethod  Method         { get; set; } = PaymentMethod.Cash;
    public string         Reference      { get; set; } = string.Empty;
    public string         Notes          { get; set; } = string.Empty;
    public ExpenseStatus  Status         { get; set; } = ExpenseStatus.Pending;
    public string         ApprovedBy     { get; set; } = string.Empty;
    public Guid           CompanyId      { get; set; }

    public string StatusLabel => Status.ToString();
}
