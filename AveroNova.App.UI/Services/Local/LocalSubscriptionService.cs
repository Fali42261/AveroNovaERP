using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Application.Interfaces;
using AveroNova.Domain.Services;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainStatus = AveroNova.Domain.Enums.SubscriptionStatus;
using UiStatus = AveroNova.App.UI.Models.SubscriptionStatus;

namespace AveroNova.App.UI.Services.Local;

public sealed class LocalSubscriptionService : ISubscriptionService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ICompanySubscriptionService _subscriptions;

    public LocalSubscriptionService(
        IDbContextFactory<AppDbContext> factory,
        ICompanySubscriptionService subscriptions)
    {
        _factory = factory;
        _subscriptions = subscriptions;
    }

    public async Task<SubscriptionModel?> GetCurrentAsync(Guid companyId)
    {
        var snapshot = await _subscriptions.GetCurrentAsync(companyId);
        if (snapshot != null)
            return MapSnapshot(snapshot);

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

    public async Task<List<SubscriptionPlanModel>> GetPlansAsync()
    {
        var plans = await _subscriptions.GetCustomerAvailablePlansAsync();
        return plans.Select(plan => new SubscriptionPlanModel
        {
            Id = plan.Code,
            Name = plan.Name,
            MonthlyPrice = plan.Price,
            YearlyPrice = plan.Price,
            Description = plan.Description,
            MaxUsers = 0,
            MaxCompanies = 0,
            Features = plan.IsTrialPlan
                ? [$"{plan.DurationInDays}-day free trial"]
                : []
        }).ToList();
    }

    public Task<List<SubscriptionPaymentModel>> GetPaymentHistoryAsync(Guid companyId)
        => Task.FromResult(new List<SubscriptionPaymentModel>());

    public Task<(bool Ok, string? Error)> UpgradeAsync(Guid companyId, string planId, BillingCycle cycle)
        => Task.FromResult<(bool, string?)>((false, "Plan upgrades are not available yet."));

    public Task<(bool Ok, string? Error)> CancelAsync(Guid companyId)
        => Task.FromResult<(bool, string?)>((false, "Cancel is not available during the trial."));

    private static SubscriptionModel MapSnapshot(Application.DTOs.CompanySubscriptionSnapshot snapshot)
    {
        return new SubscriptionModel
        {
            LocalId = snapshot.SubscriptionId,
            PlanId = snapshot.PlanCode,
            PlanName = snapshot.PlanName,
            BillingCycle = BillingCycle.Monthly,
            Price = 0m,
            StartDate = snapshot.StartDate,
            ExpiryDate = snapshot.EndDate,
            IsTrial = snapshot.IsTrial,
            TrialEndsAt = snapshot.IsTrial ? snapshot.TrialEndDate ?? snapshot.EndDate : null,
            Status = MapUiStatus(snapshot.EffectiveStatus),
            AutoRenew = snapshot.AutoRenew,
            CompanyId = snapshot.CompanyId,
            EnabledModules = snapshot.EnabledModules.ToList(),
            SyncStatus = SyncStatus.PendingSync
        };
    }

    private static SubscriptionModel Map(AveroNova.Domain.Entities.Subscription row)
    {
        var evaluated = SubscriptionStatusEvaluator.Evaluate(row, DateTime.UtcNow);
        return new SubscriptionModel
        {
            LocalId = row.Id,
            PlanId = row.PlanId?.ToString() ?? string.Empty,
            PlanName = string.IsNullOrWhiteSpace(row.PlanName) ? "Free Trial" : row.PlanName,
            BillingCycle = BillingCycle.Monthly,
            Price = row.Price,
            StartDate = row.StartDate,
            ExpiryDate = row.ExpiryDate,
            IsTrial = row.IsTrial,
            TrialEndsAt = row.IsTrial ? row.TrialEndDate ?? row.ExpiryDate : null,
            Status = MapUiStatus(evaluated),
            AutoRenew = row.AutoRenew,
            CompanyId = row.CompanyId,
            SyncStatus = SyncStatus.PendingSync
        };
    }

    private static UiStatus MapUiStatus(DomainStatus status) => status switch
    {
        DomainStatus.Active => UiStatus.Active,
        DomainStatus.Expired => UiStatus.Expired,
        DomainStatus.Cancelled => UiStatus.Cancelled,
        DomainStatus.Suspended => UiStatus.Cancelled,
        _ => UiStatus.Expired
    };
}
