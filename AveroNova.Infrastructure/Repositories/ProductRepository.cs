using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Repositories
{
    public sealed class ProductRepository : IProductRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ProductRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<Product?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty || id == Guid.Empty)
                return null;

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            return await Scoped(db, companyId)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }

        public async Task<(IReadOnlyList<Product> Items, int TotalCount)> QueryAsync(
            Guid companyId,
            string? searchText,
            int? status,
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty)
                return (Array.Empty<Product>(), 0);

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var query = Scoped(db, companyId).AsNoTracking();

            if (status.HasValue)
                query = query.Where(p => p.Status == status.Value);

            var term = searchText?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(term))
            {
                query = query.Where(p =>
                    p.Name.ToLower().Contains(term)
                    || p.SKU.ToLower().Contains(term)
                    || p.Barcode.ToLower().Contains(term));
            }

            var total = await query.CountAsync(cancellationToken);
            query = query.OrderBy(p => p.Name).ThenBy(p => p.CreatedAt);

            if (skip > 0)
                query = query.Skip(skip);
            if (take > 0)
                query = query.Take(take);

            var items = await query.ToListAsync(cancellationToken);
            return (items, total);
        }

        public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            db.Products.Add(product);
            await db.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var existing = await db.Products.FirstOrDefaultAsync(
                p => p.Id == product.Id
                     && p.CompanyId == product.CompanyId
                     && !p.IsDeleted,
                cancellationToken);
            if (existing == null)
                return;

            existing.Name = product.Name;
            existing.SKU = product.SKU;
            existing.Barcode = product.Barcode;
            existing.Category = product.Category;
            existing.Brand = product.Brand;
            existing.Unit = product.Unit;
            existing.PurchasePrice = product.PurchasePrice;
            existing.SellingPrice = product.SellingPrice;
            existing.TaxPercent = product.TaxPercent;
            existing.DiscountPercent = product.DiscountPercent;
            existing.Stock = product.Stock;
            existing.OpeningStock = product.OpeningStock;
            existing.MinimumStock = product.MinimumStock;
            existing.Description = product.Description;
            existing.Status = product.Status;
            existing.UpdatedAt = product.UpdatedAt;
            await db.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> SoftDeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty || id == Guid.Empty)
                return false;

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var existing = await db.Products.FirstOrDefaultAsync(
                p => p.Id == id && p.CompanyId == companyId && !p.IsDeleted,
                cancellationToken);
            if (existing == null)
                return false;

            existing.IsDeleted = true;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> ExistsBySkuAsync(
            Guid companyId,
            string sku,
            Guid? excludeId,
            CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty || string.IsNullOrWhiteSpace(sku))
                return false;

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var normalized = sku.Trim().ToLowerInvariant();
            var query = Scoped(db, companyId)
                .AsNoTracking()
                .Where(p => p.SKU.ToLower() == normalized);
            if (excludeId.HasValue && excludeId.Value != Guid.Empty)
                query = query.Where(p => p.Id != excludeId.Value);

            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> ExistsByBarcodeAsync(
            Guid companyId,
            string barcode,
            Guid? excludeId,
            CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty || string.IsNullOrWhiteSpace(barcode))
                return false;

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var normalized = barcode.Trim().ToLowerInvariant();
            var query = Scoped(db, companyId)
                .AsNoTracking()
                .Where(p => p.Barcode.ToLower() == normalized);
            if (excludeId.HasValue && excludeId.Value != Guid.Empty)
                query = query.Where(p => p.Id != excludeId.Value);

            return await query.AnyAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Product>> GetLowStockAsync(
            Guid companyId,
            CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty)
                return Array.Empty<Product>();

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            return await Scoped(db, companyId)
                .AsNoTracking()
                .Where(p => p.Stock <= p.MinimumStock)
                .OrderBy(p => p.Name)
                .ToListAsync(cancellationToken);
        }

        private static IQueryable<Product> Scoped(AppDbContext db, Guid companyId)
            => db.Products.Where(p => p.CompanyId == companyId && !p.IsDeleted);
    }
}
