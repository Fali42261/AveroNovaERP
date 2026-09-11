using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

public interface IBillingService
{
    Task<List<InvoiceModel>> GetAllAsync(Guid companyId);
    Task<InvoiceModel?> GetByIdAsync(Guid id);
    Task<(bool Ok, string? Error)> CreateAsync(InvoiceModel invoice);
    Task<(bool Ok, string? Error)> UpdateAsync(InvoiceModel invoice);
    Task<(bool Ok, string? Error)> DeleteAsync(Guid id);
    Task<(bool Ok, string? Error)> CancelAsync(Guid id);
    Task<(bool Ok, string? Error)> MarkPaidAsync(Guid id);
    Task<string> GetNextInvoiceNumberAsync(Guid companyId);
    Task<List<InvoiceModel>> GetByCustomerAsync(Guid customerId);
    Task<List<InvoiceModel>> GetOverdueAsync(Guid companyId);
}
