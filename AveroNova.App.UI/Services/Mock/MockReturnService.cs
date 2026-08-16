using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockReturnService : IReturnService
{
    private readonly List<SalesReturnModel>    _salesReturns    = new()
    {
        new() { LocalId = Guid.NewGuid(), ReturnNumber = "SR-2026-001",
                InvoiceNumber = "INV-2026-001", CustomerName = "TechCorp Solutions",
                ReturnDate = DateTime.Today.AddDays(-3), Reason = "Defective item",
                RefundAmount = 450m, Status = ReturnStatus.Approved, SyncStatus = SyncStatus.Synced }
    };
    private readonly List<PurchaseReturnModel> _purchaseReturns = new()
    {
        new() { LocalId = Guid.NewGuid(), ReturnNumber = "PR-2026-001",
                PurchaseNumber = "PO-2026-001", SupplierName = "Office Supplies Co.",
                ReturnDate = DateTime.Today.AddDays(-5), Reason = "Wrong item delivered",
                RefundAmount = 280m, Status = ReturnStatus.Pending, SyncStatus = SyncStatus.Synced }
    };

    public Task<List<SalesReturnModel>> GetSalesReturnsAsync(Guid companyId)
        => Task.FromResult(_salesReturns.Where(r => r.CompanyId == companyId || r.CompanyId == Guid.Empty).ToList());
    public Task<SalesReturnModel?> GetSalesReturnByIdAsync(Guid id)
        => Task.FromResult(_salesReturns.FirstOrDefault(r => r.LocalId == id));
    public Task<(bool Ok, string? Error)> CreateSalesReturnAsync(SalesReturnModel ret)
    {
        ret.LocalId = Guid.NewGuid(); ret.SyncStatus = SyncStatus.PendingSync;
        _salesReturns.Add(ret); return Task.FromResult<(bool, string?)>((true, null));
    }
    public Task<(bool Ok, string? Error)> UpdateSalesReturnAsync(SalesReturnModel ret)
    {
        var idx = _salesReturns.FindIndex(r => r.LocalId == ret.LocalId);
        if (idx < 0) return Task.FromResult((false, "Not found."));
        _salesReturns[idx] = ret; return Task.FromResult<(bool, string?)>((true, null));
    }
    public Task<(bool Ok, string? Error)> DeleteSalesReturnAsync(Guid id)
    {
        var item = _salesReturns.FirstOrDefault(r => r.LocalId == id);
        if (item == null) return Task.FromResult((false, "Not found."));
        _salesReturns.Remove(item); return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<List<PurchaseReturnModel>> GetPurchaseReturnsAsync(Guid companyId)
        => Task.FromResult(_purchaseReturns.Where(r => r.CompanyId == companyId || r.CompanyId == Guid.Empty).ToList());
    public Task<PurchaseReturnModel?> GetPurchaseReturnByIdAsync(Guid id)
        => Task.FromResult(_purchaseReturns.FirstOrDefault(r => r.LocalId == id));
    public Task<(bool Ok, string? Error)> CreatePurchaseReturnAsync(PurchaseReturnModel ret)
    {
        ret.LocalId = Guid.NewGuid(); ret.SyncStatus = SyncStatus.PendingSync;
        _purchaseReturns.Add(ret); return Task.FromResult<(bool, string?)>((true, null));
    }
    public Task<(bool Ok, string? Error)> UpdatePurchaseReturnAsync(PurchaseReturnModel ret)
    {
        var idx = _purchaseReturns.FindIndex(r => r.LocalId == ret.LocalId);
        if (idx < 0) return Task.FromResult((false, "Not found."));
        _purchaseReturns[idx] = ret; return Task.FromResult<(bool, string?)>((true, null));
    }
    public Task<(bool Ok, string? Error)> DeletePurchaseReturnAsync(Guid id)
    {
        var item = _purchaseReturns.FirstOrDefault(r => r.LocalId == id);
        if (item == null) return Task.FromResult((false, "Not found."));
        _purchaseReturns.Remove(item); return Task.FromResult<(bool, string?)>((true, null));
    }
}
