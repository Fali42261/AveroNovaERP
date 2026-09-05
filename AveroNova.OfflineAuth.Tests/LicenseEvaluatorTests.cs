using AveroNova.Domain.Constants;
using AveroNova.Domain.Enums;
using AveroNova.Domain.Licensing;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class LicenseEvaluatorTests
{
    [Fact]
    public void ClockRollback_DoesNotExtendEffectiveTime()
    {
        var server = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);
        var rolledBack = server.AddDays(-10);
        var effective = LicenseEvaluator.GetEffectiveUtc(rolledBack, server);
        Assert.Equal(server, effective);
    }

    [Fact]
    public void Trial_AllowsAccess_BeforeEnd()
    {
        var start = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(LicenseConstants.TrialDays);
        var now = start.AddDays(14);
        Assert.True(LicenseEvaluator.AllowsAccess(LicenseStatus.Trial, true, end, end, now));
        Assert.Equal(1, LicenseEvaluator.GetRemainingTrialDays(end, now));
    }

    [Fact]
    public void Trial_BlocksAccess_AfterEnd_EvenIfOfflineClockRolledBack()
    {
        var start = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(LicenseConstants.TrialDays);
        var lastServer = end.AddHours(2);
        var rolledBack = start.AddDays(1);
        var effective = LicenseEvaluator.GetEffectiveUtc(rolledBack, lastServer);
        Assert.False(LicenseEvaluator.AllowsAccess(LicenseStatus.Trial, true, end, end, effective));
        Assert.Equal(LicenseStatus.Expired, LicenseEvaluator.ResolveStatus(LicenseStatus.Trial, true, end, end, effective));
    }
}
