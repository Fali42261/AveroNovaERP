using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Persistence
{
    public static class SubscriptionCatalogSeeder
    {
        public static readonly Guid FreeTrialPlanId = Guid.Parse("b1111111-0000-4000-8000-000000000001");
        public static readonly Guid ProPlanId = Guid.Parse("b1111111-0000-4000-8000-000000000002");
        public static readonly Guid BusinessPlanId = Guid.Parse("b1111111-0000-4000-8000-000000000003");
        public static readonly Guid EnterprisePlanId = Guid.Parse("b1111111-0000-4000-8000-000000000004");

        public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            await EnsurePlanAsync(db, new SubscriptionPlan
            {
                Id = FreeTrialPlanId,
                Code = SubscriptionPlanCodes.FreeTrial,
                Name = "Free Trial",
                Description = "15-day Free Trial with currently available AveroNova modules.",
                DurationInDays = FreeTrialSubscriptionFactory.DefaultDurationDays,
                IsTrialPlan = true,
                IsCustomerAvailable = true,
                IsActive = true,
                SortOrder = 1,
                Price = 0m,
                CreatedAt = now
            }, cancellationToken);

            await EnsurePlanAsync(db, new SubscriptionPlan
            {
                Id = ProPlanId,
                Code = SubscriptionPlanCodes.Pro,
                Name = "Pro",
                Description = "Future Pro plan. Not available to customers yet.",
                DurationInDays = 30,
                IsTrialPlan = false,
                IsCustomerAvailable = false,
                IsActive = false,
                SortOrder = 2,
                Price = 0m,
                CreatedAt = now
            }, cancellationToken);

            await EnsurePlanAsync(db, new SubscriptionPlan
            {
                Id = BusinessPlanId,
                Code = SubscriptionPlanCodes.Business,
                Name = "Business",
                Description = "Future Business plan. Not available to customers yet.",
                DurationInDays = 30,
                IsTrialPlan = false,
                IsCustomerAvailable = false,
                IsActive = false,
                SortOrder = 3,
                Price = 0m,
                CreatedAt = now
            }, cancellationToken);

            await EnsurePlanAsync(db, new SubscriptionPlan
            {
                Id = EnterprisePlanId,
                Code = SubscriptionPlanCodes.Enterprise,
                Name = "Enterprise",
                Description = "Future Enterprise plan. Not available to customers yet.",
                DurationInDays = 365,
                IsTrialPlan = false,
                IsCustomerAvailable = false,
                IsActive = false,
                SortOrder = 4,
                Price = 0m,
                CreatedAt = now
            }, cancellationToken);

            foreach (var planId in new[] { FreeTrialPlanId, ProPlanId, BusinessPlanId, EnterprisePlanId })
            {
                var enableFeatures = planId == FreeTrialPlanId;
                foreach (var moduleKey in SubscriptionModules.Catalog)
                {
                    var exists = await db.SubscriptionPlanFeatures.AnyAsync(
                        f => f.PlanId == planId && f.ModuleKey == moduleKey, cancellationToken);
                    if (exists)
                        continue;

                    db.SubscriptionPlanFeatures.Add(new SubscriptionPlanFeature
                    {
                        Id = Guid.NewGuid(),
                        PlanId = planId,
                        ModuleKey = moduleKey,
                        ModuleName = SubscriptionModules.DisplayName(moduleKey),
                        IsEnabled = enableFeatures,
                        CreatedAt = now,
                        IsDeleted = false
                    });
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            await BackfillAsync(db, cancellationToken);
        }

        public static async Task BackfillAsync(AppDbContext db, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var freeTrial = await db.SubscriptionPlans
                .FirstAsync(p => p.Code == SubscriptionPlanCodes.FreeTrial, cancellationToken);

            var companies = await db.Companies.Where(c => !c.IsDeleted).ToListAsync(cancellationToken);
            foreach (var company in companies)
            {
                var linked = await db.UserCompanies.AnyAsync(
                    uc => uc.UserId == company.UserId && uc.CompanyId == company.Id && !uc.IsDeleted,
                    cancellationToken);
                if (!linked)
                {
                    db.UserCompanies.Add(UserCompanyFactory.CreateOwner(company.UserId, company.Id, now));
                }
            }

            var subscriptions = await db.Subscriptions.Where(s => !s.IsDeleted).ToListAsync(cancellationToken);
            foreach (var subscription in subscriptions)
            {
                if (subscription.PlanId == null || subscription.PlanId == Guid.Empty)
                    subscription.PlanId = freeTrial.Id;

                if (string.IsNullOrWhiteSpace(subscription.PlanName)
                    || string.Equals(subscription.PlanName, "Starter", StringComparison.OrdinalIgnoreCase))
                    subscription.PlanName = freeTrial.Name;

                if (subscription.SubscriptionType == 0)
                    subscription.SubscriptionType = SubscriptionType.Trial;

                if (!subscription.IsTrial)
                    subscription.IsTrial = subscription.Price == 0m
                        || subscription.Plan == SubscriptionDuration.FifteenDays
                        || subscription.Plan == SubscriptionDuration.Trial
                        || string.Equals(subscription.PlanName, "Free Trial", StringComparison.OrdinalIgnoreCase);

                subscription.TrialStartDate ??= subscription.StartDate;
                subscription.TrialEndDate ??= subscription.ExpiryDate;
                SubscriptionStatusEvaluator.ApplyEvaluatedState(subscription, now);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        private static async Task EnsurePlanAsync(
            AppDbContext db,
            SubscriptionPlan plan,
            CancellationToken cancellationToken)
        {
            var existing = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == plan.Code, cancellationToken);
            if (existing == null)
            {
                db.SubscriptionPlans.Add(plan);
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            existing.Name = plan.Name;
            existing.Description = plan.Description;
            existing.DurationInDays = plan.DurationInDays;
            existing.IsTrialPlan = plan.IsTrialPlan;
            existing.IsCustomerAvailable = plan.IsCustomerAvailable;
            existing.IsActive = plan.IsActive;
            existing.SortOrder = plan.SortOrder;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
