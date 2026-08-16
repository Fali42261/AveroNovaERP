using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.Infrastructure.Repositories
{
    public class PlanRepository : IPlanRepository
    {
        private readonly AppDbContext _context;

        public PlanRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Plan plan)
        {
            await _context.Plans.AddAsync(plan);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Plan plan)
        {
            plan.IsDeleted = true;
            _context.Plans.Update(plan);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Plan>> GetAllAsync()
        {
            return await _context.Plans
                .Where(x => !x.IsDeleted)
                .ToListAsync();
        }

        public async Task<List<Plan>> GetAllActiveAsync()
        {
            return await _context.Plans
                .Where(x => x.IsActive && !x.IsDeleted)
                .ToListAsync();
        }

        public async Task<Plan?> GetByIdAsync(Guid id)
        {
            return await _context.Plans
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        }

        public async Task<Plan?> GetByNameAsync(string name)
        {
            return await _context.Plans
                .FirstOrDefaultAsync(x => x.Name == name && !x.IsDeleted);
        }

        public async Task UpdateAsync(Plan plan)
        {
            _context.Plans.Update(plan);
            await _context.SaveChangesAsync();
        }
    }
}
