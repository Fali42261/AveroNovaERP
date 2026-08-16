using System;
using System.Collections.Generic;
using System.Text;
using AveroNova.Domain.Entities;

namespace AveroNova.Application.Interfaces.Repositories
{
    public interface IPlanRepository
    {
        Task<Plan?> GetByIdAsync(Guid id);
        Task<Plan?> GetByNameAsync(string name);
        Task<List<Plan>> GetAllActiveAsync();
        Task<List<Plan>> GetAllAsync();
        Task AddAsync(Plan plan);
        Task UpdateAsync(Plan plan);
        Task DeleteAsync(Plan plan);
    }
}
