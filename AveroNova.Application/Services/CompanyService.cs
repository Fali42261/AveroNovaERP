using AveroNova.Application.Interfaces;
using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Domain.Entities;

namespace AveroNova.Application.Services
{
    public class CompanyService : ICompanyService
    {
        private readonly ICompanyRepository _companyRepository;
        private readonly IUserCompanyRepository _userCompanyRepository;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IPlanService _planService;

        public CompanyService(
            ICompanyRepository companyRepository,
            IUserCompanyRepository userCompanyRepository,
            ISubscriptionService subscriptionService,
            IPlanService planService)
        {
            _companyRepository = companyRepository;
            _userCompanyRepository = userCompanyRepository;
            _subscriptionService = subscriptionService;
            _planService = planService;
        }

        public async Task AddAsync(Company company)
        {
            if (company.Id == Guid.Empty)
            {
                company.Id = Guid.NewGuid();
            }

            if (company.CreatedAt == default)
            {
                company.CreatedAt = DateTime.UtcNow;
            }

            await _companyRepository.AddAsync(company);
            await CreateFreeTrialSubscriptionAsync(company.Id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var company = await _companyRepository.GetByIdAsync(id);

            if (company != null)
            {
                await _companyRepository.DeleteAsync(company);
            }
        }

        public async Task<List<Company>> GetAllAsync()
        {
            return await _companyRepository.GetAllAsync();
        }

        public async Task<Company?> GetByIdAsync(Guid id)
        {
            return await _companyRepository.GetByIdAsync(id);
        }

        public async Task UpdateAsync(Company company)
        {
            company.UpdatedAt = DateTime.UtcNow;
            await _companyRepository.UpdateAsync(company);
        }

        public async Task AddUserToCompanyAsync(Guid userId, Guid companyId, bool isOwner)
        {
            var existing = await _userCompanyRepository.GetByUserAndCompanyAsync(userId, companyId);
            if (existing != null)
            {
                existing.IsActive = true;
                existing.IsOwner = isOwner;
                existing.IsDeleted = false;
                existing.UpdatedAt = DateTime.UtcNow;
                await _userCompanyRepository.UpdateAsync(existing);
                return;
            }

            var membership = new UserCompany
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CompanyId = companyId,
                IsOwner = isOwner,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _userCompanyRepository.AddAsync(membership);
        }

        public async Task<List<Company>> GetCompaniesForUserAsync(Guid userId)
        {
            var memberships = await _userCompanyRepository.GetByUserIdAsync(userId);
            return memberships
                .Where(m => m.Company != null)
                .Select(m => m.Company)
                .ToList();
        }

        public async Task<List<User>> GetUsersForCompanyAsync(Guid companyId)
        {
            var memberships = await _userCompanyRepository.GetByCompanyIdAsync(companyId);
            return memberships
                .Where(m => m.User != null)
                .Select(m => m.User)
                .ToList();
        }

        private async Task CreateFreeTrialSubscriptionAsync(Guid companyId)
        {
            var freeTrialPlan = await _planService.GetFreeTrialPlanAsync();

            if (freeTrialPlan == null)
            {
                freeTrialPlan = Plan.CreateFreeTrialCatalog();
                await _planService.AddAsync(freeTrialPlan);
            }

            await _subscriptionService.CreateFromPlanAsync(
                companyId,
                freeTrialPlan.Id,
                isTrial: true);
        }
    }
}
