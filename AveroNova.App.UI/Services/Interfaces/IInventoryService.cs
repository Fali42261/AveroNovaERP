using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

// ═══════════════════════════════════════════════════════════════
//  IInventoryService
//
//  ONLINE:  Inventory changes synchronize through the API.
//  OFFLINE: Inventory operations are persisted locally and added
//           to the pending synchronization queue.
//
//  TODO: Implement inventory synchronization during backend phase.
// ═══════════════════════════════════════════════════════════════

public interface IInventoryService
{
    Task<List<InventoryItemModel>>   GetInventoryAsync(Guid companyId);
    Task<InventoryItemModel?>        GetByProductIdAsync(Guid productId);
    Task<List<StockMovementModel>>   GetMovementsAsync(Guid companyId, Guid? productId = null);
    Task<(bool Ok, string? Error)>   AdjustStockAsync(StockAdjustmentModel adjustment);
}
