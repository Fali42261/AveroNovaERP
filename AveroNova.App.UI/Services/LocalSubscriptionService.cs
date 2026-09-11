using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using RecordSyncStatus = AveroNova.Domain.Enums.RecordSyncStatus;
using SyncOperation = AveroNova.Domain.Enums.SyncOperation;

namespace AveroNova.App.UI.Services;

public sealed class LocalSubscriptionService : ISubscriptionService
{
    // Registration launch policy:
    // Free is the only selectable/usable plan for now. Paid plans stay visible
    // in Step 3 so customers can understand what is coming, but they cannot
    // select them until paid subscriptions are enabled.
    private static readonly SubscriptionPlanModel[] Plans =
    [
        new()
        {
            Id = "starter",
            Name = "Free",
            Description = "Free plan for early customers while AveroNova is being tested.",
            MonthlyPrice = 0,
            YearlyPrice = 0,
            MaxUsers = 2,
            MaxCompanies = 1,
            TrialDays = 0,
            CurrencyCode = "INR",
            IsAvailable = true,
            Features = ["1 Company", "2 Users", "Basic Invoicing", "Customer Management", "Offline-first access"]
        },
        new()
        {
            Id = "business",
            Name = "Business",
            Description = "Paid business features will be enabled after customer validation.",
            MonthlyPrice = 999,
            YearlyPrice = 9999,
            MaxUsers = 10,
            MaxCompanies = 3,
            TrialDays = 0,
            CurrencyCode = "INR",
            IsPopular = true,
            IsAvailable = false,
            Features = ["3 Companies", "10 Users", "Inventory", "Purchases", "Reports"]
        },
        new()
        {
            Id = "enterprise",
            Name = "Enterprise",
            Description = "Advanced plan preview. Contact/upgrade will be enabled later.",
            MonthlyPrice = 2499,
            YearlyPrice = 24999,
            MaxUsers = 50,
            MaxCompanies = 10,
            TrialDays = 0,
            CurrencyCode = "INR",
            IsAvailable = false,
            Features = ["10 Companies", "50 Users", "API Access", "Custom Roles", "Priority Support"]
        }
    ];

    private readonly IDbContextFactory<LocalAppDbContext> _factory;
    private readonly IAppSessionContext _session;

    public LocalSubscriptionService(IDbContextFactory<LocalAppDbContext> factory, IAppSessionContext session)
    {
        _factory = factory;
        _session = session;
    }

    public async Task<SubscriptionModel?> GetCurrentAsync(Guid companyId)
    {
        if (!Allows(companyId)) return null;
        await using var db = await _factory.CreateDbContextAsync();
        var x = await db.Subscriptions.AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .OrderByDescending(s => s.StartDateUtc)
            .FirstOrDefaultAsync();
        return x is null ? null : Map(x);
    }

    public Task<SubscriptionModel?> GetCurrentAsync() =>
        _session.CurrentCompanyId is Guid cid
            ? GetCurrentAsync(cid)
            : Task.FromResult<SubscriptionModel?>(null);

    public async Task<List<SubscriptionPlanModel>> GetPlansAsync()
    {
        var current = await GetCurrentAsync();
        return Plans.Select(p => new SubscriptionPlanModel
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            MonthlyPrice = p.MonthlyPrice,
            YearlyPrice = p.YearlyPrice,
            Features = [.. p.Features],
            IsPopular = p.IsPopular,
            IsAvailable = p.IsAvailable,
            MaxUsers = p.MaxUsers,
            MaxCompanies = p.MaxCompanies,
            TrialDays = p.TrialDays,
            CurrencyCode = p.CurrencyCode,
            IsCurrentPlan = current?.PlanId == p.Id
        }).ToList();
    }

    public async Task<List<SubscriptionPaymentModel>> GetPaymentHistoryAsync(Guid companyId)
    {
        if (!Allows(companyId)) return [];
        await using var db = await _factory.CreateDbContextAsync();
        return await db.SubscriptionPayments.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.PaymentDate)
            .Select(x => new SubscriptionPaymentModel
            {
                LocalId = x.Id,
                ServerId = x.ServerId.HasValue ? x.ServerId.Value.ToString("D") : null,
                CompanyId = x.CompanyId,
                PaymentNumber = x.PaymentNumber,
                PlanName = x.PlanName,
                Amount = x.Amount,
                PaymentDate = x.PaymentDate,
                Method = x.Method,
                Status = x.Status,
                Invoice = x.Invoice,
                CreatedAt = x.CreatedAtUtc,
                UpdatedAt = x.UpdatedAtUtc,
                LastSyncedAt = x.LastSyncedAtUtc,
                SyncStatus = x.SyncStatus == (int)RecordSyncStatus.Synced ? SyncStatus.Synced : SyncStatus.PendingSync
            }).ToListAsync();
    }

    public async Task<(bool Ok, string? Error)> UpgradeAsync(Guid companyId, string planId, BillingCycle cycle)
    {
        if (!Allows(companyId)) return (false, "You do not have access to this company.");

        var plan = Plans.FirstOrDefault(x =>
            x.Id.Equals(planId, StringComparison.OrdinalIgnoreCase) && x.IsAvailable);

        if (plan is null)
            return (false, "This plan is preview-only right now. Please continue with the Free plan.");

        // Safety rule: until business subscriptions are launched, only the Free
        // plan can ever be activated locally even if a future plan is accidentally
        // marked available in UI data.
        if (!plan.Id.Equals("starter", StringComparison.OrdinalIgnoreCase))
            return (false, "Paid plans are not enabled yet. Please use the Free plan.");

        if (!Enum.IsDefined(cycle)) return (false, "Invalid billing cycle.");

        await using var db = await _factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var x = await db.Subscriptions
            .Where(s => s.CompanyId == companyId)
            .OrderByDescending(s => s.StartDateUtc)
            .FirstOrDefaultAsync();

        var created = x is null;
        if (x is null)
        {
            x = new LocalSubscriptionEntity { Id = Guid.NewGuid(), CompanyId = companyId, StartDateUtc = now };
            db.Subscriptions.Add(x);
        }

        x.PlanId = plan.Id;
        x.PlanName = plan.Name;
        x.BillingCycle = (int)cycle;
        x.Price = 0;
        x.StartDateUtc = now;
        x.EndDateUtc = now.AddYears(10);
        x.IsTrial = false;
        x.IsActive = true;
        x.Status = (int)SubscriptionStatus.Active;
        x.AutoRenew = false;
        x.MaxUsers = plan.MaxUsers;
        x.MaxCompanies = plan.MaxCompanies;
        x.MaxStorageMB = 500;
        x.SyncStatus = (int)RecordSyncStatus.Pending;
        x.UpdatedAtUtc = now;

        var number = await NextPaymentNumberAsync(db, companyId);
        var payment = new LocalSubscriptionPaymentEntity
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PaymentNumber = number,
            PlanName = plan.Name,
            Amount = 0,
            PaymentDate = now,
            Method = "—",
            Status = "Free",
            Invoice = "—",
            SyncStatus = (int)RecordSyncStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.SubscriptionPayments.Add(payment);
        LocalSyncQueueWriter.Enqueue(db, "Subscription", x.Id, companyId,
            created ? SyncOperation.Create : SyncOperation.Update, Payload(x), now);
        LocalSyncQueueWriter.Enqueue(db, "SubscriptionPayment", payment.Id, companyId,
            SyncOperation.Create,
            new { payment.Id, payment.CompanyId, payment.PaymentNumber, payment.PlanName, payment.Amount, payment.Status }, now);

        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> CancelAsync(Guid companyId)
    {
        if (!Allows(companyId)) return (false, "You do not have access to this company.");
        await using var db = await _factory.CreateDbContextAsync();
        var x = await db.Subscriptions.Where(s => s.CompanyId == companyId)
            .OrderByDescending(s => s.StartDateUtc)
            .FirstOrDefaultAsync();
        if (x is null) return (false, "Subscription not found.");
        if ((SubscriptionStatus)x.Status == SubscriptionStatus.Cancelled)
            return (false, "Subscription is already cancelled.");

        x.Status = (int)SubscriptionStatus.Cancelled;
        x.AutoRenew = false;
        x.IsActive = false;
        x.SyncStatus = (int)RecordSyncStatus.Pending;
        x.UpdatedAtUtc = DateTime.UtcNow;
        LocalSyncQueueWriter.Enqueue(db, "Subscription", x.Id, companyId,
            SyncOperation.Update, Payload(x), x.UpdatedAtUtc);
        await db.SaveChangesAsync();
        return (true, null);
    }

    private static async Task<string> NextPaymentNumberAsync(LocalAppDbContext db, Guid cid)
    {
        var prefix = $"SUB-{DateTime.Today:yyyy}-";
        var all = await db.SubscriptionPayments
            .Where(x => x.CompanyId == cid && x.PaymentNumber.StartsWith(prefix))
            .Select(x => x.PaymentNumber)
            .ToListAsync();
        var max = all.Select(x => int.TryParse(x[prefix.Length..], out var n) ? n : 0)
            .DefaultIfEmpty().Max();
        return $"{prefix}{max + 1:D4}";
    }

    private bool Allows(Guid cid) => cid != Guid.Empty && _session.CurrentCompanyId == cid;

    private static SubscriptionModel Map(LocalSubscriptionEntity x) => new()
    {
        LocalId = x.Id,
        CompanyId = x.CompanyId,
        PlanId = x.PlanId,
        PlanName = x.PlanName,
        BillingCycle = (BillingCycle)x.BillingCycle,
        Price = x.Price,
        StartDate = x.StartDateUtc,
        ExpiryDate = x.EndDateUtc,
        IsTrial = x.IsTrial,
        TrialEndsAt = x.IsTrial ? x.EndDateUtc : null,
        Status = (SubscriptionStatus)x.Status,
        AutoRenew = x.AutoRenew,
        MaxUsers = x.MaxUsers,
        MaxCompanies = x.MaxCompanies,
        MaxStorageMB = x.MaxStorageMB,
        SyncStatus = x.SyncStatus == (int)RecordSyncStatus.Synced ? SyncStatus.Synced : SyncStatus.PendingSync,
        UpdatedAt = x.UpdatedAtUtc
    };

    private static object Payload(LocalSubscriptionEntity x) => new
    {
        x.Id, x.CompanyId, x.PlanId, x.PlanName, x.BillingCycle, x.Price,
        x.StartDateUtc, x.EndDateUtc, x.IsTrial, x.Status, x.AutoRenew,
        x.MaxUsers, x.MaxCompanies, x.MaxStorageMB
    };
}
