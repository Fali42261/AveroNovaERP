using AveroNova.Domain.Entities;

namespace AveroNova.Application.Interfaces.Repositories
{
    public interface ICustomerRepository
    {
        Task<Customer?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);

        Task<(IReadOnlyList<Customer> Items, int TotalCount)> QueryAsync(
            Guid companyId,
            string? searchText,
            int? status,
            int skip,
            int take,
            CancellationToken cancellationToken = default);

        Task AddAsync(Customer customer, CancellationToken cancellationToken = default);

        Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default);

        Task<bool> SoftDeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    }
}
