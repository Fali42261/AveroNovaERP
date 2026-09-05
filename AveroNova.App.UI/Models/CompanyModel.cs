namespace AveroNova.App.UI.Models;

// ═══════════════════════════════════════════════════════════════
//  AVERONOVA ERP — COMPANY MODEL
// ═══════════════════════════════════════════════════════════════

public class CompanyModel : BaseModel
{
    public string   Name            { get; set; } = string.Empty;
    public string   Email           { get; set; } = string.Empty;
    public string   Phone           { get; set; } = string.Empty;
    public string   Address         { get; set; } = string.Empty;
    public string   City            { get; set; } = string.Empty;
    public string   Country         { get; set; } = string.Empty;
    public string   TaxNumber       { get; set; } = string.Empty;
    public string   RegistrationNo  { get; set; } = string.Empty;
    public string   Currency        { get; set; } = "USD";
    public string   CurrencySymbol  { get; set; } = "$";
    public string   LogoUrl         { get; set; } = string.Empty;
    public string   InvoicePrefix   { get; set; } = "INV";
    public string   Website         { get; set; } = string.Empty;
    public CompanyStatus Status     { get; set; } = CompanyStatus.Active;
    public bool     IsCurrentCompany { get; set; }

    public string Initials => Name.Length > 0
        ? string.Join("", Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2).Select(w => w[0].ToString().ToUpper()))
        : "?";
}
