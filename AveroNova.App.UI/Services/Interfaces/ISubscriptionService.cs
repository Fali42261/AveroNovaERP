using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

public interface ISubscriptionService
{
    Task<SubscriptionModel?>           GetCurrentAsync(Guid companyId);
    Task<List<SubscriptionPlanModel>>  GetPlansAsync();
    Task<List<SubscriptionPaymentModel>> GetPaymentHistoryAsync(Guid companyId);
    Task<(bool Ok, string? Error)>     UpgradeAsync(Guid companyId, string planId, BillingCycle cycle);
    Task<(bool Ok, string? Error)>     CancelAsync(Guid companyId);
    Task<SubscriptionModel?> GetCurrentAsync();
}
