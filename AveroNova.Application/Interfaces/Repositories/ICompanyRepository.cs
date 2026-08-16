using System;
using System.Collections.Generic;
using System.Text;
using AveroNova.Domain.Entities;

namespace AveroNova.Application.Interfaces.Repositories
{
    public interface ICompanyRepository
    {
        Task<Company?> GetByIdAsync(Guid id);
        Task<List<Company>> GetAllAsync();
        Task AddAsync(Company company);
        Task UpdateAsync(Company company);
        Task DeleteAsync(Company company);

    }
}
