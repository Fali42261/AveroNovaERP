using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Repositories
{
    public class CompanyRepository : ICompanyRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CompanyRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task AddAsync(Company company)
        {
            await using var db = await _factory.CreateDbContextAsync();
            await db.Companies.AddAsync(company);
            await db.SaveChangesAsync();
        }

        public async Task DeleteAsync(Company company)
        {
            await using var db = await _factory.CreateDbContextAsync();
            var existing = await db.Companies.FirstOrDefaultAsync(
                c => c.Id == company.Id && !c.IsDeleted);
            if (existing == null)
                return;

            existing.IsDeleted = true;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        public async Task<List<Company>> GetAllAsync()
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.Companies.AsNoTracking()
                .Where(c => !c.IsDeleted)
                .ToListAsync();
        }

        public async Task<Company?> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
                return null;

            await using var db = await _factory.CreateDbContextAsync();
            return await db.Companies.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }

        public async Task UpdateAsync(Company company)
        {
            await using var db = await _factory.CreateDbContextAsync();
            var existing = await db.Companies.FirstOrDefaultAsync(
                c => c.Id == company.Id && !c.IsDeleted);
            if (existing == null)
                return;

            existing.OwnerName = company.OwnerName;
            existing.GSTNumber = company.GSTNumber;
            existing.PANNumber = company.PANNumber;
            existing.Email = company.Email;
            existing.MobileNumber = company.MobileNumber;
            existing.Address = company.Address;
            existing.City = company.City;
            existing.State = company.State;
            existing.Country = company.Country;
            existing.PinCode = company.PinCode;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }
}
