using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

public interface IReturnService
{
    Task<List<SalesReturnModel>>    GetSalesReturnsAsync(Guid companyId);
    Task<SalesReturnModel?>         GetSalesReturnByIdAsync(Guid id);
    Task<(bool Ok, string? Error)>  CreateSalesReturnAsync(SalesReturnModel ret);
    Task<(bool Ok, string? Error)>  UpdateSalesReturnAsync(SalesReturnModel ret);
    Task<(bool Ok, string? Error)>  DeleteSalesReturnAsync(Guid id);

    Task<List<PurchaseReturnModel>> GetPurchaseReturnsAsync(Guid companyId);
    Task<PurchaseReturnModel?>      GetPurchaseReturnByIdAsync(Guid id);
    Task<(bool Ok, string? Error)>  CreatePurchaseReturnAsync(PurchaseReturnModel ret);
    Task<(bool Ok, string? Error)>  UpdatePurchaseReturnAsync(PurchaseReturnModel ret);
    Task<(bool Ok, string? Error)>  DeletePurchaseReturnAsync(Guid id);
}
