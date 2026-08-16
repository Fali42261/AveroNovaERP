using AveroNova.Application.Interfaces;
using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.Application.Services
{
    public class CompanyService : ICompanyService
    {
        private readonly ICompanyRepository _companyRepository;

        public CompanyService(ICompanyRepository companyRepository)
        {
            _companyRepository = companyRepository;
        }
       
        public async Task AddAsync(Company company)
        {
            await _companyRepository.AddAsync(company);
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
            await _companyRepository.UpdateAsync(company);
        }
    }
}
