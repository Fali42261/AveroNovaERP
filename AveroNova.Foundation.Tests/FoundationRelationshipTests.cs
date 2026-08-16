using Xunit;
using AveroNova.Application.Interfaces;
using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Application.Services;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using AveroNova.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AveroNova.Foundation.Tests
{
    public class FoundationRelationshipTests
    {
        private static ServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

            services.AddScoped<ICompanyRepository, CompanyRepository>();
            services.AddScoped<IUserCompanyRepository, UserCompanyRepository>();
            services.AddScoped<IPlanRepository, PlanRepository>();
            services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
            services.AddScoped<ICompanyService, CompanyService>();
            services.AddScoped<IPlanService, PlanService>();
            services.AddScoped<ISubscriptionService, SubscriptionService>();
            services.AddScoped<ICreditService, CreditService>();

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task User_Can_Belong_To_Multiple_Companies()
        {
            await using var provider = BuildProvider();
            var db = provider.GetRequiredService<AppDbContext>();
            var companyService = provider.GetRequiredService<ICompanyService>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                UserCode = "U001",
                FullName = "Owner One",
                Email = "owner@averonova.test",
                PasswordHash = "hash",
                IsActiveUser = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var companyA = CreateCompany("CMPA", "Company A");
            var companyB = CreateCompany("CMPB", "Company B");
            await companyService.AddAsync(companyA);
            await companyService.AddAsync(companyB);

            await companyService.AddUserToCompanyAsync(user.Id, companyA.Id, isOwner: true);
            await companyService.AddUserToCompanyAsync(user.Id, companyB.Id, isOwner: true);

            var companies = await companyService.GetCompaniesForUserAsync(user.Id);

            Assert.Equal(2, companies.Count);
            Assert.Contains(companies, c => c.Id == companyA.Id);
            Assert.Contains(companies, c => c.Id == companyB.Id);
        }

        [Fact]
        public async Task Company_Can_Have_Multiple_Users()
        {
            await using var provider = BuildProvider();
            var db = provider.GetRequiredService<AppDbContext>();
            var companyService = provider.GetRequiredService<ICompanyService>();

            var userA = new User
            {
                Id = Guid.NewGuid(),
                UserCode = "U010",
                FullName = "User A",
                Email = "a@averonova.test",
                PasswordHash = "hash",
                IsActiveUser = true,
                CreatedAt = DateTime.UtcNow
            };
            var userB = new User
            {
                Id = Guid.NewGuid(),
                UserCode = "U011",
                FullName = "User B",
                Email = "b@averonova.test",
                PasswordHash = "hash",
                IsActiveUser = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.AddRange(userA, userB);
            await db.SaveChangesAsync();

            var company = CreateCompany("CMPC", "Company C");
            await companyService.AddAsync(company);

            await companyService.AddUserToCompanyAsync(userA.Id, company.Id, isOwner: true);
            await companyService.AddUserToCompanyAsync(userB.Id, company.Id, isOwner: false);

            var users = await companyService.GetUsersForCompanyAsync(company.Id);

            Assert.Equal(2, users.Count);
            Assert.Contains(users, u => u.Id == userA.Id);
            Assert.Contains(users, u => u.Id == userB.Id);
        }

        [Fact]
        public async Task Company_Subscription_Uses_Plan_Trial_And_Zero_CreditsUsed()
        {
            await using var provider = BuildProvider();
            var companyService = provider.GetRequiredService<ICompanyService>();
            var subscriptionService = provider.GetRequiredService<ISubscriptionService>();
            var planService = provider.GetRequiredService<IPlanService>();

            var company = CreateCompany("CMPD", "Company D");
            await companyService.AddAsync(company);

            var plan = await planService.GetFreeTrialPlanAsync();
            Assert.NotNull(plan);
            Assert.True(plan.TrialDays > 0);
            Assert.True(plan.CreditLimit > 0);

            var subscription = await subscriptionService.GetByCompanyIdAsync(company.Id);
            Assert.NotNull(subscription);
            Assert.Equal(company.Id, subscription.CompanyId);
            Assert.Equal(plan.Id, subscription.PlanId);
            Assert.Equal(0, subscription.CreditsUsed);
            Assert.Equal(plan.CreditLimit, subscription.CreditLimit);
            Assert.Equal(plan.CalculatePeriodEndDate(subscription.StartDate), subscription.EndDate);
            Assert.Equal(plan.CreditLimit, subscription.RemainingCredits);
        }

        [Fact]
        public void Plan_EndDate_Is_StartDate_Plus_TrialDays()
        {
            var start = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc);
            var plan = Plan.CreateFreeTrialCatalog();

            var end = plan.CalculatePeriodEndDate(start);

            Assert.Equal(start.AddDays(plan.TrialDays), end);
        }

        private static Company CreateCompany(string code, string name)
        {
            return new Company
            {
                Id = Guid.NewGuid(),
                CompanyCode = code,
                CompanyName = name,
                OwnerName = "Owner",
                Email = $"{code.ToLowerInvariant()}@averonova.test",
                MobileNumber = "9999999999",
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
