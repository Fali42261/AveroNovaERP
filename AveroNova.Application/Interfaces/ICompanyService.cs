using AveroNova.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.Application.Interfaces
{
    public interface ICompanyService
    {
        Task<List<Company>> GetAllAsync();

        Task<Company?> GetByIdAsync(Guid id);

        Task AddAsync(Company company);

        Task UpdateAsync(Company company);

        Task DeleteAsync(Guid id);
    }
}
