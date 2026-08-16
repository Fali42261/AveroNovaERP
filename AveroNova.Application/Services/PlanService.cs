using AveroNova.Application.Interfaces;
using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;

namespace AveroNova.Application.Services
{
    public class PlanService : IPlanService
    {
        private readonly IPlanRepository _planRepository;

        public PlanService(IPlanRepository planRepository)
        {
            _planRepository = planRepository;
        }

        public async Task AddAsync(Plan plan)
        {
            await _planRepository.AddAsync(plan);
        }

        public async Task DeleteAsync(Guid id)
        {
            var plan = await _planRepository.GetByIdAsync(id);
            if (plan != null)
            {
                await _planRepository.DeleteAsync(plan);
            }
        }

        public async Task<List<Plan>> GetAllAsync()
        {
            return await _planRepository.GetAllAsync();
        }

        public async Task<List<Plan>> GetAllActiveAsync()
        {
            return await _planRepository.GetAllActiveAsync();
        }

        public async Task<Plan?> GetByIdAsync(Guid id)
        {
            return await _planRepository.GetByIdAsync(id);
        }

        public async Task<Plan?> GetByNameAsync(string name)
        {
            return await _planRepository.GetByNameAsync(name);
        }

        public async Task<Plan?> GetFreeTrialPlanAsync()
        {
            return await _planRepository.GetByNameAsync(PlanNames.FreeTrial);
        }

        public async Task UpdateAsync(Plan plan)
        {
            plan.UpdatedAt = DateTime.UtcNow;
            await _planRepository.UpdateAsync(plan);
        }
    }
}
