using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Constants;

namespace AveroNova.App.UI.Services.Mock;

public class MockSubscriptionService : ISubscriptionService
{
    public Task<SubscriptionModel?> GetCurrentAsync(Guid companyId) => Task.FromResult<SubscriptionModel?>(
        new SubscriptionModel
        {
            PlanId = SubscriptionPlanCodes.FreeTrial,
            PlanName = "Free Trial",
            BillingCycle = BillingCycle.Monthly,
            Price = 0m,
            StartDate = DateTime.UtcNow.Date,
            ExpiryDate = DateTime.UtcNow.Date.AddDays(15),
            IsTrial = true,
            TrialEndsAt = DateTime.UtcNow.Date.AddDays(15),
            Status = SubscriptionStatus.Active,
            AutoRenew = false,
            CompanyId = companyId
        });

    public Task<List<SubscriptionPlanModel>> GetPlansAsync() => Task.FromResult(new List<SubscriptionPlanModel>
    {
        new()
        {
            Id = SubscriptionPlanCodes.FreeTrial,
            Name = "Free Trial",
            MonthlyPrice = 0m,
            YearlyPrice = 0m,
            Description = "15-day Free Trial with currently available AveroNova modules.",
            MaxUsers = 0,
            MaxCompanies = 0,
            Features = ["15-day free trial"]
        }
    });

    public Task<List<SubscriptionPaymentModel>> GetPaymentHistoryAsync(Guid companyId)
        => Task.FromResult(new List<SubscriptionPaymentModel>());

    public Task<(bool Ok, string? Error)> UpgradeAsync(Guid companyId, string planId, BillingCycle cycle)
        => Task.FromResult<(bool, string?)>((false, "Plan upgrades are not available yet."));

    public Task<(bool Ok, string? Error)> CancelAsync(Guid companyId)
        => Task.FromResult<(bool, string?)>((false, "Cancel is not available during the trial."));

    public Task<SubscriptionModel?> GetCurrentAsync()
        => GetCurrentAsync(Guid.Empty);
}
