using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Entities;

public sealed class Customer : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int Status { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal TotalPurchases { get; set; }

    public void ApplyUpdate(
        string name,
        string? email,
        string? phone,
        string? address,
        string? city,
        string? country,
        string? taxNumber,
        string? notes,
        int status,
        decimal outstandingBalance,
        decimal totalPurchases)
    {
        Name = name.Trim();
        Email = email?.Trim() ?? string.Empty;
        Phone = phone?.Trim() ?? string.Empty;
        Address = address?.Trim() ?? string.Empty;
        City = city?.Trim() ?? string.Empty;
        Country = country?.Trim() ?? string.Empty;
        TaxNumber = taxNumber?.Trim() ?? string.Empty;
        Notes = notes?.Trim() ?? string.Empty;
        Status = status;
        OutstandingBalance = outstandingBalance;
        TotalPurchases = totalPurchases;
        MarkPendingChange();
    }
}