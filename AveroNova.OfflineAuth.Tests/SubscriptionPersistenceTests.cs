using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class SubscriptionPersistenceTests : IAsyncLifetime
{
    private string _path = null!;
    private IDbContextFactory<LocalAppDbContext> _factory = null!;
    private LocalSubscriptionService _service = null!;
    private readonly Guid _company = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _path = Path.Combine(Path.GetTempPath(), $"averonova-subscription-{Guid.NewGuid():N}.db");
        var o = new DbContextOptionsBuilder<LocalAppDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options;

        _factory = new Factory(o);
        await using (var db = await _factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
            db.Subscriptions.Add(new LocalSubscriptionEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = _company,
                PlanId = "starter",
                PlanName = "Free",
                StartDateUtc = DateTime.UtcNow.AddDays(-2),
                EndDateUtc = DateTime.UtcNow.AddYears(10),
                IsTrial = false,
                IsActive = true,
                Status = (int)SubscriptionStatus.Active,
                MaxUsers = 2,
                MaxCompanies = 1,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var session = new AppSessionContext();
        session.SetFromLocal(
            new LocalUserEntity { Id = Guid.NewGuid(), FullName = "Subscriber", Email = "sub@test.local" },
            new LocalCompanyEntity { Id = _company, CompanyName = "Subscriber Co" },
            ["Owner"],
            ["subscription.view"],
            Guid.NewGuid());

        _service = new LocalSubscriptionService(_factory, session);
    }

    public Task DisposeAsync()
    {
        try
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
        catch { }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task PaidPlans_ArePreviewOnly_AndFreePlanRemainsActive()
    {
        var current = await _service.GetCurrentAsync(_company);
        Assert.NotNull(current);
        Assert.Equal("starter", current.PlanId);

        var business = await _service.UpgradeAsync(_company, "business", BillingCycle.Yearly);
        Assert.False(business.Ok);
        Assert.Contains("preview-only", business.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var enterprise = await _service.UpgradeAsync(_company, "enterprise", BillingCycle.Monthly);
        Assert.False(enterprise.Ok);

        var unchanged = await _service.GetCurrentAsync();
        Assert.NotNull(unchanged);
        Assert.Equal("starter", unchanged.PlanId);

        var history = await _service.GetPaymentHistoryAsync(_company);
        Assert.Empty(history);
    }

    [Fact]
    public async Task PlansAndCompanyValidation_AreEnforced()
    {
        var plans = await _service.GetPlansAsync();
        Assert.Equal(3, plans.Count);

        var free = plans.Single(x => x.Id == "starter");
        Assert.True(free.IsCurrentPlan);
        Assert.True(free.IsAvailable);
        Assert.Equal("Free", free.Name);
        Assert.Equal(0, free.TrialDays);

        Assert.False(plans.Single(x => x.Id == "business").IsAvailable);
        Assert.False(plans.Single(x => x.Id == "enterprise").IsAvailable);

        Assert.Null(await _service.GetCurrentAsync(Guid.NewGuid()));
        Assert.Empty(await _service.GetPaymentHistoryAsync(Guid.NewGuid()));
        Assert.False((await _service.UpgradeAsync(Guid.NewGuid(), "business", BillingCycle.Monthly)).Ok);
        Assert.False((await _service.UpgradeAsync(_company, "missing", BillingCycle.Monthly)).Ok);
    }

    private sealed class Factory(DbContextOptions<LocalAppDbContext> o) : IDbContextFactory<LocalAppDbContext>
    {
        public LocalAppDbContext CreateDbContext() => new(o);

        public Task<LocalAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
