using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

public interface IPurchaseService
{
    Task<List<PurchaseModel>> GetAllAsync(Guid companyId);
    Task<PurchaseModel?>      GetByIdAsync(Guid id);
    Task<(bool Ok, string? Error)> CreateAsync(PurchaseModel purchase);
    Task<(bool Ok, string? Error)> UpdateAsync(PurchaseModel purchase);
    Task<(bool Ok, string? Error)> DeleteAsync(Guid id);
    Task<string>                   GetNextPurchaseNumberAsync(Guid companyId);
}
