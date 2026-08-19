using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

/// <summary>
/// Dashboard aggregation. Implementations must use existing module services
/// (billing, products, customers, payments, company, auth) — not direct DB access.
/// Swap this implementation to call the Development API later without changing the UI.
/// </summary>
public interface IDashboardService
{
    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
