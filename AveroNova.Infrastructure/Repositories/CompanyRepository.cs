using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.Infrastructure.Repositories
{
    public class CompanyRepository : ICompanyRepository
    {
        private readonly AppDbContext _context;

        public CompanyRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task AddAsync(Company company)
        {
            await _context.Companies.AddAsync(company);
            //var countBefore = await _context.Companies.CountAsync();
            await _context.SaveChangesAsync();

            //var countAfter = await _context.Companies.CountAsync();

            //Console.WriteLine($"Before : {countBefore}");
            //Console.WriteLine($"After  : {countAfter}");
        }

        public async Task DeleteAsync(Company company)
        {
            company.IsDeleted = false;
            _context.Companies.Update(company);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Company>> GetAllAsync()
        {
            var reult = await _context.Companies.ToListAsync();
            return reult;
        }

        public async Task<Company?> GetByIdAsync(Guid id)
        {
            return await _context.Companies.FindAsync(id);
        }

        public async Task UpdateAsync(Company company)
        {
            _context.Companies.Update(company);
            await _context.SaveChangesAsync();
        }
    }
}
