using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Repositories
{
    public class UserCompanyRepository : IUserCompanyRepository
    {
        private readonly AppDbContext _context;

        public UserCompanyRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(UserCompany userCompany)
        {
            await _context.UserCompanies.AddAsync(userCompany);
            await _context.SaveChangesAsync();
        }

        public async Task<UserCompany?> GetByUserAndCompanyAsync(Guid userId, Guid companyId)
        {
            return await _context.UserCompanies
                .Include(x => x.User)
                .Include(x => x.Company)
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.CompanyId == companyId &&
                    !x.IsDeleted);
        }

        public async Task<List<UserCompany>> GetByUserIdAsync(Guid userId)
        {
            return await _context.UserCompanies
                .Include(x => x.Company)
                .Where(x => x.UserId == userId && x.IsActive && !x.IsDeleted)
                .ToListAsync();
        }

        public async Task<List<UserCompany>> GetByCompanyIdAsync(Guid companyId)
        {
            return await _context.UserCompanies
                .Include(x => x.User)
                .Where(x => x.CompanyId == companyId && x.IsActive && !x.IsDeleted)
                .ToListAsync();
        }

        public async Task UpdateAsync(UserCompany userCompany)
        {
            _context.UserCompanies.Update(userCompany);
            await _context.SaveChangesAsync();
        }
    }
}
