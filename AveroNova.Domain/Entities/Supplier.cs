using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Entities;

public sealed class Supplier : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public void ApplyUpdate(string name, string? email, string? phone, string? address, string? taxNumber, string? notes, bool isActive)
    {
        Name = name.Trim();
        Email = email?.Trim() ?? string.Empty;
        Phone = phone?.Trim() ?? string.Empty;
        Address = address?.Trim() ?? string.Empty;
        TaxNumber = taxNumber?.Trim() ?? string.Empty;
        Notes = notes?.Trim() ?? string.Empty;
        IsActive = isActive;
        MarkPendingChange();
    }
}
