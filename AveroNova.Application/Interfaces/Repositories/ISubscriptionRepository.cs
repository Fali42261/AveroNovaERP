using System;
using System.Collections.Generic;
using System.Text;
using AveroNova.Domain.Entities;

namespace AveroNova.Application.Interfaces.Repositories
{
    public interface ISubscriptionRepository
    {
        Task<Subscription?> GetByIdAsync(Guid id);
        Task<Subscription?> GetByCompanyIdAsync(Guid companyId);
        Task<List<Subscription>> GetAllAsync();
        Task AddAsync(Subscription subscription);
        Task UpdateAsync(Subscription subscription);
        Task DeleteAsync(Subscription subscription);
    }
}
