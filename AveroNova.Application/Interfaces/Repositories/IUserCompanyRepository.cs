using AveroNova.Domain.Entities;

namespace AveroNova.Application.Interfaces.Repositories
{
    public interface IUserCompanyRepository
    {
        Task<UserCompany?> GetByUserAndCompanyAsync(Guid userId, Guid companyId);
        Task<List<UserCompany>> GetByUserIdAsync(Guid userId);
        Task<List<UserCompany>> GetByCompanyIdAsync(Guid companyId);
        Task AddAsync(UserCompany userCompany);
        Task UpdateAsync(UserCompany userCompany);
    }
}
