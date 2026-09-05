namespace AveroNova.App.UI.Models;

// ═══════════════════════════════════════════════════════════════
//  AVERONOVA ERP — CUSTOMER MODEL
// ═══════════════════════════════════════════════════════════════

public class CustomerModel : BaseModel
{
    public string   Name            { get; set; } = string.Empty;
    public string   Email           { get; set; } = string.Empty;
    public string   Phone           { get; set; } = string.Empty;
    public string   Address         { get; set; } = string.Empty;
    public string   City            { get; set; } = string.Empty;
    public string   Country         { get; set; } = string.Empty;
    public string   TaxNumber       { get; set; } = string.Empty;
    public string   Notes           { get; set; } = string.Empty;
    public CustomerStatus Status    { get; set; } = CustomerStatus.Active;
    public decimal  OutstandingBalance { get; set; }
    public decimal  TotalPurchases   { get; set; }
    public Guid     CompanyId        { get; set; }

    public string Initials => Name.Length > 0
        ? string.Join("", Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2).Select(w => w[0].ToString().ToUpper()))
        : "?";

    public string StatusLabel => Status switch
    {
        CustomerStatus.Active   => "Active",
        CustomerStatus.Inactive => "Inactive",
        CustomerStatus.Blocked  => "Blocked",
        _                       => "Unknown"
    };
}
