using AveroNova.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.Application.Interfaces
{
    public interface IPlanService
    {
        Task<Plan?> GetByIdAsync(Guid id);
        Task<Plan?> GetByNameAsync(string name);
        Task<List<Plan>> GetAllActiveAsync();
        Task<List<Plan>> GetAllAsync();
        Task AddAsync(Plan plan);
        Task UpdateAsync(Plan plan);
        Task DeleteAsync(Guid id);
        Task<Plan?> GetFreeTrialPlanAsync();
    }
}
