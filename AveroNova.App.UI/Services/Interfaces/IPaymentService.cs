using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

// ═══════════════════════════════════════════════════════════════
//  IPaymentService
//
//  OFFLINE: Cash/payment records may be created locally
//           and synchronized when connectivity is restored.
//  TODO: Implement PaymentService + local persistence + SyncQueue
//        during backend phase.
// ═══════════════════════════════════════════════════════════════

public interface IPaymentService
{
    Task<List<PaymentModel>> GetAllAsync(Guid companyId);
    Task<PaymentModel?>      GetByIdAsync(Guid id);
    Task<(bool Ok, string? Error)> CreateAsync(PaymentModel payment);
    Task<(bool Ok, string? Error)> UpdateAsync(PaymentModel payment);
    Task<(bool Ok, string? Error)> DeleteAsync(Guid id);
    Task<string>                   GetNextPaymentNumberAsync(Guid companyId);
}
