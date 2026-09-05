using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockInventoryService : IInventoryService
{
    private static readonly List<InventoryItemModel> _inventory = MockDataStore.Products
        .Select(p => new InventoryItemModel
        {
            ProductId      = p.LocalId,
            ProductName    = p.Name,
            SKU            = p.SKU,
            Category       = p.Category,
            CurrentStock   = p.Stock,
            AvailableStock = p.Stock,
            ReservedStock  = 0,
            MinimumStock   = p.MinimumStock,
            CompanyId      = p.CompanyId,
            SyncStatus     = p.SyncStatus
        }).ToList();

    private static readonly List<StockMovementModel> _movements = new()
    {
        new() { ProductName = "Office Desk Pro", SKU = "DESK-001", Type = StockMovementType.In,
                Quantity = 10, StockBefore = 15, StockAfter = 25, Reference = "PO-001",
                Notes = "Received from supplier", CreatedBy = "Admin" },
        new() { ProductName = "Ergonomic Chair", SKU = "CHAIR-002", Type = StockMovementType.Out,
                Quantity = 2, StockBefore = 5, StockAfter = 3, Reference = "INV-2026-001",
                Notes = "Sold", CreatedBy = "Admin" },
        new() { ProductName = "Wireless Mouse", SKU = "MOUSE-004", Type = StockMovementType.Adjustment,
                Quantity = -3, StockBefore = 5, StockAfter = 2, Reference = "ADJ-001",
                Notes = "Physical count adjustment", CreatedBy = "Admin" },
    };

    public Task<List<InventoryItemModel>> GetInventoryAsync(Guid companyId)
        => Task.FromResult(_inventory.Where(i => i.CompanyId == companyId).ToList());

    public Task<InventoryItemModel?> GetByProductIdAsync(Guid productId)
        => Task.FromResult(_inventory.FirstOrDefault(i => i.ProductId == productId));

    public Task<List<StockMovementModel>> GetMovementsAsync(Guid companyId, Guid? productId = null)
    {
        var result = productId.HasValue
            ? _movements.Where(m => m.ProductId == productId.Value).ToList()
            : _movements.ToList();
        return Task.FromResult(result);
    }

    public Task<(bool Ok, string? Error)> AdjustStockAsync(StockAdjustmentModel adjustment)
    {
        var item = _inventory.FirstOrDefault(i => i.ProductId == adjustment.ProductId);
        if (item == null) return Task.FromResult((false, "Inventory item not found."));
        var before = item.CurrentStock;
        item.CurrentStock   = adjustment.NewStock;
        item.AvailableStock = adjustment.NewStock;
        _movements.Insert(0, new StockMovementModel
        {
            ProductId   = adjustment.ProductId,
            ProductName = adjustment.ProductName,
            Type        = StockMovementType.Adjustment,
            Quantity    = adjustment.Difference,
            StockBefore = before,
            StockAfter  = adjustment.NewStock,
            Notes       = adjustment.Reason,
            CreatedBy   = adjustment.AdjustedBy,
            CompanyId   = adjustment.CompanyId,
            SyncStatus  = SyncStatus.PendingSync
        });
        return Task.FromResult<(bool, string?)>((true, null));
    }
}
