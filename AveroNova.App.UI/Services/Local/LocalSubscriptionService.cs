using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainStatus = AveroNova.Domain.Enums.SubscriptionStatus;
using UiStatus = AveroNova.App.UI.Models.SubscriptionStatus;

namespace AveroNova.App.UI.Services.Local;

public sealed class LocalSubscriptionService : ISubscriptionService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public LocalSubscriptionService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<SubscriptionModel?> GetCurrentAsync(Guid companyId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Subscriptions
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId && !s.IsDeleted)
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync();
        return row == null ? null : Map(row);
    }

    public Task<SubscriptionModel?> GetCurrentAsync()
    {
        var companyId = LocalSessionStore.CompanyId;
        return companyId == null
            ? Task.FromResult<SubscriptionModel?>(null)
            : GetCurrentAsync(companyId.Value);
    }

    public Task<List<SubscriptionPlanModel>> GetPlansAsync() => Task.FromResult(new List<SubscriptionPlanModel>
    {
        new()
        {
            Id = "starter",
            Name = "Starter",
            MonthlyPrice = 0m,
            YearlyPrice = 0m,
            Description = "15-day free trial for getting started",
            MaxUsers = 2,
            MaxCompanies = 1,
            Features = ["1 Company", "2 Users", "15-day free trial", "Basic invoicing", "Offline-first"]
        },
        new()
        {
            Id = "business",
            Name = "Business",
            MonthlyPrice = 0m,
            YearlyPrice = 0m,
            Description = "For growing businesses",
            MaxUsers = 10,
            MaxCompanies = 3,
            Features = ["Coming soon"]
        },
        new()
        {
            Id = "enterprise",
            Name = "Enterprise",
            MonthlyPrice = 0m,
            YearlyPrice = 0m,
            Description = "For established organizations",
            MaxUsers = -1,
            MaxCompanies = -1,
            Features = ["Coming soon"]
        }
    });

    public Task<List<SubscriptionPaymentModel>> GetPaymentHistoryAsync(Guid companyId)
        => Task.FromResult(new List<SubscriptionPaymentModel>());

    public Task<(bool Ok, string? Error)> UpgradeAsync(Guid companyId, string planId, BillingCycle cycle)
        => Task.FromResult<(bool, string?)>((false, "Plan upgrades are not available yet."));

    public Task<(bool Ok, string? Error)> CancelAsync(Guid companyId)
        => Task.FromResult<(bool, string?)>((false, "Cancel is not available during the trial."));

    private static SubscriptionModel Map(AveroNova.Domain.Entities.Subscription row)
    {
        var isTrial = row.Price == 0m || row.Plan == AveroNova.Domain.Enums.SubscriptionPlan.FifteenDays
            || string.Equals(row.PlanName, "Starter", StringComparison.OrdinalIgnoreCase);

        return new SubscriptionModel
        {
            LocalId = row.Id,
            PlanId = row.PlanName.ToLowerInvariant().Replace(" ", "-"),
            PlanName = row.PlanName,
            BillingCycle = BillingCycle.Monthly,
            Price = row.Price,
            StartDate = row.StartDate,
            ExpiryDate = row.ExpiryDate,
            IsTrial = isTrial,
            TrialEndsAt = isTrial ? row.ExpiryDate : null,
            Status = MapStatus(row.Status, row.ExpiryDate),
            AutoRenew = false,
            CompanyId = row.CompanyId,
            MaxUsers = 2,
            MaxCompanies = 1,
            MaxStorageMB = 500,
            SyncStatus = SyncStatus.PendingSync
        };
    }

    private static UiStatus MapStatus(DomainStatus status, DateTime expiry)
    {
        if (expiry.Date < DateTime.UtcNow.Date)
            return UiStatus.Expired;
        return status == DomainStatus.Active ? UiStatus.Active : UiStatus.Cancelled;
    }
}
