using AveroNova.Domain.Entities;

namespace AveroNova.Domain.Services
{
    public static class UserCompanyFactory
    {
        public static UserCompany CreateOwner(Guid userId, Guid companyId, DateTime utcNow)
        {
            return new UserCompany
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CompanyId = companyId,
                IsOwner = true,
                IsActive = true,
                CreatedAt = utcNow,
                IsDeleted = false
            };
        }
    }
}
