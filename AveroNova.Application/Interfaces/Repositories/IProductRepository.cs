using AveroNova.Domain.Entities;

namespace AveroNova.Application.Interfaces.Repositories
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);

        Task<(IReadOnlyList<Product> Items, int TotalCount)> QueryAsync(
            Guid companyId,
            string? searchText,
            int? status,
            int skip,
            int take,
            CancellationToken cancellationToken = default);

        Task AddAsync(Product product, CancellationToken cancellationToken = default);

        Task UpdateAsync(Product product, CancellationToken cancellationToken = default);

        Task<bool> SoftDeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);

        Task<bool> ExistsBySkuAsync(
            Guid companyId,
            string sku,
            Guid? excludeId,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByBarcodeAsync(
            Guid companyId,
            string barcode,
            Guid? excludeId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Product>> GetLowStockAsync(Guid companyId, CancellationToken cancellationToken = default);
    }
}
