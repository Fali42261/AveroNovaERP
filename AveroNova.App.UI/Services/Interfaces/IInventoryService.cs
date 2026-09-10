using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

// ═══════════════════════════════════════════════════════════════
//  IInventoryService
//
//  ONLINE:  Inventory changes synchronize through the API.
//  OFFLINE: Inventory operations are persisted locally and added
//           to the pending synchronization queue.
//
//  Local-first implementations queue inventory changes for server synchronization.
// ═══════════════════════════════════════════════════════════════

public interface IInventoryService
{
    Task<List<InventoryItemModel>>   GetInventoryAsync(Guid companyId);
    Task<InventoryItemModel?>        GetByProductIdAsync(Guid productId);
    Task<List<StockMovementModel>>   GetMovementsAsync(Guid companyId, Guid? productId = null);
    Task<(bool Ok, string? Error)>   AdjustStockAsync(StockAdjustmentModel adjustment);
}
