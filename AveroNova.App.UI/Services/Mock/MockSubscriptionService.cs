using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockSubscriptionService : ISubscriptionService
{
    public Task<SubscriptionModel?> GetCurrentAsync(Guid companyId) => Task.FromResult<SubscriptionModel?>(
        new SubscriptionModel
        {
            PlanId        = "pro",
            PlanName      = "Professional",
            BillingCycle  = BillingCycle.Monthly,
            Price         = 49.99m,
            StartDate     = DateTime.Today.AddDays(-45),
            ExpiryDate    = DateTime.Today.AddDays(15),
            IsTrial       = false,
            Status        = SubscriptionStatus.Active,
            AutoRenew     = true,
            CompanyId     = companyId
        });

    public Task<List<SubscriptionPlanModel>> GetPlansAsync() => Task.FromResult(new List<SubscriptionPlanModel>
    {
        new() { Id = "starter",  Name = "Starter",      MonthlyPrice = 0m,     YearlyPrice = 0m,
                Description = "For individuals and small teams",
                MaxUsers = 2, MaxCompanies = 1,
                Features = ["1 Company", "2 Users", "Basic Invoicing", "Customer Management", "Community Support"] },
        new() { Id = "pro",      Name = "Professional", MonthlyPrice = 49.99m, YearlyPrice = 499.99m,
                Description = "For growing businesses",
                IsPopular = true, IsCurrentPlan = true,
                MaxUsers = 10, MaxCompanies = 3,
                Features = ["3 Companies", "10 Users", "Full Billing & POS", "Inventory Management",
                            "Purchase Management", "Reports & Analytics", "Priority Support"] },
        new() { Id = "business", Name = "Business",     MonthlyPrice = 99.99m, YearlyPrice = 999.99m,
                Description = "For established businesses",
                MaxUsers = 50, MaxCompanies = 10,
                Features = ["10 Companies", "50 Users", "Everything in Pro", "Advanced Reports",
                            "API Access", "Custom Roles", "Dedicated Support"] },
        new() { Id = "enterprise", Name = "Enterprise", MonthlyPrice = 0m,     YearlyPrice = 0m,
                Description = "Custom pricing for large organizations",
                MaxUsers = -1, MaxCompanies = -1,
                Features = ["Unlimited Companies", "Unlimited Users", "White-label", "Custom Integrations",
                            "SLA Support", "On-premise option"] },
    });

    public Task<List<SubscriptionPaymentModel>> GetPaymentHistoryAsync(Guid companyId) =>
        Task.FromResult(new List<SubscriptionPaymentModel>
        {
            new() { PaymentNumber = "SUB-2026-003", PlanName = "Professional", Amount = 49.99m,
                    PaymentDate = DateTime.Today.AddDays(-15), Method = "Credit Card", Status = "Paid", Invoice = "SUBINV-003" },
            new() { PaymentNumber = "SUB-2026-002", PlanName = "Professional", Amount = 49.99m,
                    PaymentDate = DateTime.Today.AddDays(-45), Method = "Credit Card", Status = "Paid", Invoice = "SUBINV-002" },
            new() { PaymentNumber = "SUB-2026-001", PlanName = "Starter",      Amount = 0m,
                    PaymentDate = DateTime.Today.AddDays(-75), Method = "—",           Status = "Free",  Invoice = "—" },
        });

    public Task<(bool Ok, string? Error)> UpgradeAsync(Guid companyId, string planId, BillingCycle cycle)
        => Task.FromResult<(bool, string?)>((true, null));

    public Task<(bool Ok, string? Error)> CancelAsync(Guid companyId)
        => Task.FromResult<(bool, string?)>((true, null));

    public Task<SubscriptionModel?> GetCurrentAsync()
    {
        return Task.FromResult<SubscriptionModel?>(new SubscriptionModel
        {
            PlanId = "pro",
            PlanName = "Professional",
            BillingCycle = BillingCycle.Monthly,
            Price = 999,
            StartDate = DateTime.Today.AddDays(-10),
            ExpiryDate = DateTime.Today.AddDays(20),
            Status = SubscriptionStatus.Active,
            AutoRenew = true,
            IsTrial = false,
            MaxUsers = 10,
            MaxCompanies = 3,
            MaxStorageMB = 500
        });
    }
}
