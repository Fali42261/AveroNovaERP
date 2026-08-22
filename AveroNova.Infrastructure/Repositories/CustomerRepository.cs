using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Repositories
{
    public sealed class CustomerRepository : ICustomerRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CustomerRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<Customer?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty || id == Guid.Empty)
                return null;

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            return await Scoped(db, companyId)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<(IReadOnlyList<Customer> Items, int TotalCount)> QueryAsync(
            Guid companyId,
            string? searchText,
            int? status,
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty)
                return (Array.Empty<Customer>(), 0);

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var query = Scoped(db, companyId).AsNoTracking();

            if (status.HasValue)
                query = query.Where(c => c.Status == status.Value);

            var term = searchText?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(term))
            {
                query = query.Where(c =>
                    c.Name.ToLower().Contains(term)
                    || c.MobileNumber.ToLower().Contains(term)
                    || c.Email.ToLower().Contains(term));
            }

            var total = await query.CountAsync(cancellationToken);
            query = query.OrderBy(c => c.Name).ThenBy(c => c.CreatedAt);

            if (skip > 0)
                query = query.Skip(skip);
            if (take > 0)
                query = query.Take(take);

            var items = await query.ToListAsync(cancellationToken);
            return (items, total);
        }

        public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            db.Customers.Add(customer);
            await db.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var existing = await db.Customers.FirstOrDefaultAsync(
                c => c.Id == customer.Id
                     && c.CompanyId == customer.CompanyId
                     && !c.IsDeleted,
                cancellationToken);
            if (existing == null)
                return;

            existing.Name = customer.Name;
            existing.MobileNumber = customer.MobileNumber;
            existing.Email = customer.Email;
            existing.Address = customer.Address;
            existing.City = customer.City;
            existing.State = customer.State;
            existing.Country = customer.Country;
            existing.PinCode = customer.PinCode;
            existing.TaxNumber = customer.TaxNumber;
            existing.Notes = customer.Notes;
            existing.Status = customer.Status;
            existing.UpdatedAt = customer.UpdatedAt;
            await db.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> SoftDeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
        {
            if (companyId == Guid.Empty || id == Guid.Empty)
                return false;

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var existing = await db.Customers.FirstOrDefaultAsync(
                c => c.Id == id && c.CompanyId == companyId && !c.IsDeleted,
                cancellationToken);
            if (existing == null)
                return false;

            existing.IsDeleted = true;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        private static IQueryable<Customer> Scoped(AppDbContext db, Guid companyId)
            => db.Customers.Where(c => c.CompanyId == companyId && !c.IsDeleted);
    }
}
