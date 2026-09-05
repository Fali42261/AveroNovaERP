using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockPurchaseService : IPurchaseService
{
    private int _counter = 2;
    private readonly List<PurchaseModel> _purchases = new()
    {
        new() { LocalId = Guid.NewGuid(), PurchaseNumber = "PO-2026-001",
                SupplierName = "Office Supplies Co.", PurchaseDate = DateTime.Today.AddDays(-10),
                DueDate = DateTime.Today.AddDays(20), Status = PurchaseStatus.Received,
                PaidAmount = 1200m, CompanyId = MockDataStore.CompanyId1, SyncStatus = SyncStatus.Synced },
        new() { LocalId = Guid.NewGuid(), PurchaseNumber = "PO-2026-002",
                SupplierName = "TechDistrib Ltd.", PurchaseDate = DateTime.Today.AddDays(-3),
                DueDate = DateTime.Today.AddDays(27), Status = PurchaseStatus.Ordered,
                PaidAmount = 0m, CompanyId = MockDataStore.CompanyId1, SyncStatus = SyncStatus.Synced },
    };

    public Task<List<PurchaseModel>> GetAllAsync(Guid companyId)
        => Task.FromResult(_purchases.Where(p => p.CompanyId == companyId).ToList());

    public Task<PurchaseModel?> GetByIdAsync(Guid id)
        => Task.FromResult(_purchases.FirstOrDefault(p => p.LocalId == id));

    public Task<(bool Ok, string? Error)> CreateAsync(PurchaseModel purchase)
    {
        purchase.LocalId = Guid.NewGuid(); purchase.SyncStatus = SyncStatus.PendingSync;
        _purchases.Add(purchase); return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> UpdateAsync(PurchaseModel purchase)
    {
        var idx = _purchases.FindIndex(p => p.LocalId == purchase.LocalId);
        if (idx < 0) return Task.FromResult((false, "Purchase not found."));
        purchase.SyncStatus = SyncStatus.PendingSync;
        _purchases[idx] = purchase; return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        var item = _purchases.FirstOrDefault(p => p.LocalId == id);
        if (item == null) return Task.FromResult((false, "Purchase not found."));
        _purchases.Remove(item); return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<string> GetNextPurchaseNumberAsync(Guid companyId)
        => Task.FromResult($"PO-2026-{_counter++:D3}");
}
