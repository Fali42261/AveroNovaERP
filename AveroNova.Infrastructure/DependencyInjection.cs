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
            services.AddDbContextFactory<AppDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));
            services.AddScoped(sp =>
                sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

            services.AddScoped<ICompanyRepository, CompanyRepository>();
            services.AddScoped<ICompanyService, CompanyService>();
            services.AddScoped<ISubscriptionAccessRepository, SubscriptionAccessRepository>();
            services.AddScoped<ICompanySubscriptionService, CompanySubscriptionService>();
            services.AddScoped<IAccessControlService, AccessControlService>();

            return services;
        }
    }
}
