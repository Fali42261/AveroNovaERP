using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Entities;

public sealed class Payment : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public Guid PartyId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public bool IsSupplier { get; set; }
    public Guid? InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Method { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int Status { get; set; }

    public void ApplyUpdate(string paymentNumber, Guid partyId, string partyName, bool isSupplier,
        Guid? invoiceId, string invoiceNumber, decimal amount, int method, DateTime paymentDate,
        string? reference, string? notes, int status)
    {
        PaymentNumber = paymentNumber.Trim(); PartyId = partyId; PartyName = partyName.Trim(); IsSupplier = isSupplier;
        InvoiceId = invoiceId; InvoiceNumber = invoiceNumber.Trim(); Amount = amount; Method = method; PaymentDate = paymentDate;
        Reference = reference?.Trim() ?? string.Empty; Notes = notes?.Trim() ?? string.Empty; Status = status;
        MarkPendingChange();
    }
}
