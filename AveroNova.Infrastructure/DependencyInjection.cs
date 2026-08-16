using AveroNova.Application.Interfaces;
using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Application.Services;
using AveroNova.Infrastructure.Persistence;
using AveroNova.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services, 
            string dbPath) 
        {
            services.AddDbContext<AppDbContext>(options =>
             options.UseSqlite($"Data Source={dbPath}"));

            services.AddScoped<ICompanyRepository, CompanyRepository>();
            services.AddScoped<ICompanyService, CompanyService>();

            return services;
        }
    }
}
