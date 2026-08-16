using AveroNova.Application.Interfaces;
using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Application.Services;
using AveroNova.Infrastructure.Persistence;
using AveroNova.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
            services.AddScoped<IUserCompanyRepository, UserCompanyRepository>();
            services.AddScoped<IPlanRepository, PlanRepository>();
            services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();

            services.AddScoped<ICompanyService, CompanyService>();
            services.AddScoped<IPlanService, PlanService>();
            services.AddScoped<ISubscriptionService, SubscriptionService>();
            services.AddScoped<ICreditService, CreditService>();

            return services;
        }
    }
}
