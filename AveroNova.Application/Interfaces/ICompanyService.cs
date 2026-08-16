using AveroNova.Domain.Entities;

namespace AveroNova.Application.Interfaces
{
    public interface ICompanyService
    {
        Task<List<Company>> GetAllAsync();
        Task<Company?> GetByIdAsync(Guid id);
        Task AddAsync(Company company);
        Task UpdateAsync(Company company);
        Task DeleteAsync(Guid id);
        Task AddUserToCompanyAsync(Guid userId, Guid companyId, bool isOwner);
        Task<List<Company>> GetCompaniesForUserAsync(Guid userId);
        Task<List<User>> GetUsersForCompanyAsync(Guid companyId);
    }
}
