namespace AveroNova.App.UI.Models;

public class SubscriptionPlanModel
{
    public string   Id            { get; set; } = string.Empty;
    public string   Name          { get; set; } = string.Empty;
    public string   Description   { get; set; } = string.Empty;
    public decimal  MonthlyPrice  { get; set; }
    public decimal  YearlyPrice   { get; set; }
    public List<string> Features  { get; set; } = [];
    public bool     IsPopular      { get; set; }
    public bool     IsCurrentPlan  { get; set; }
    public int      MaxUsers       { get; set; }
    public int      MaxCompanies   { get; set; }

}

public class SubscriptionModel : BaseModel
{
    public string PlanId { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
    public decimal Price { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool IsTrial { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public bool AutoRenew { get; set; } = true;
    public Guid CompanyId { get; set; }

    public bool IsExpired =>
        ExpiryDate < DateTime.Today;

    public bool IsActive =>
        Status == SubscriptionStatus.Active && !IsExpired;

    public int DaysRemaining =>
        Math.Max(0, (ExpiryDate - DateTime.Today).Days);

    public string StatusLabel =>
        Status.ToString();

    public string BillingLabel =>
        BillingCycle == BillingCycle.Monthly
            ? "Monthly"
            : "Yearly";

    public int MaxUsers { get; set; }
    public int MaxCompanies { get; set; }
    public int MaxStorageMB { get; set; } = 500;
}

public class SubscriptionPaymentModel : BaseModel
{
    public string   PaymentNumber { get; set; } = string.Empty;
    public string   PlanName      { get; set; } = string.Empty;
    public decimal  Amount        { get; set; }
    public DateTime PaymentDate   { get; set; }
    public string   Method        { get; set; } = string.Empty;
    public string   Status        { get; set; } = string.Empty;
    public string   Invoice       { get; set; } = string.Empty;
    public Guid     CompanyId     { get; set; }
}
