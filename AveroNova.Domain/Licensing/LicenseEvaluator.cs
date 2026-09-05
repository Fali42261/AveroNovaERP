using AveroNova.Domain.Constants;
using AveroNova.Domain.Enums;

namespace AveroNova.Domain.Licensing;

/// <summary>
/// Pure license rules shared by server and client. Server time is authoritative for creation;
/// the client uses last-known server time as a floor against obvious clock rollback.
/// </summary>
public static class LicenseEvaluator
{
    public static DateTime GetEffectiveUtc(DateTime deviceUtc, DateTime? lastKnownTrustedTimeUtc)
    {
        var device = EnsureUtc(deviceUtc);
        if (lastKnownTrustedTimeUtc is not DateTime trusted)
            return device;

        var known = EnsureUtc(trusted);
        if (device + LicenseConstants.ClockRollbackTolerance < known)
            return known;

        return device < known ? known : device;
    }

    public static bool IsObviousClockRollback(DateTime deviceUtc, DateTime? lastKnownTrustedTimeUtc)
    {
        if (lastKnownTrustedTimeUtc is not DateTime trusted)
            return false;

        return EnsureUtc(deviceUtc) + LicenseConstants.ClockRollbackTolerance < EnsureUtc(trusted);
    }

    public static DateTime AdvanceTrustedWatermark(DateTime deviceUtc, DateTime? lastKnownTrustedTimeUtc)
    {
        var device = EnsureUtc(deviceUtc);
        if (lastKnownTrustedTimeUtc is not DateTime trusted)
            return device;

        var known = EnsureUtc(trusted);
        return device > known ? device : known;
    }

    public static LicenseStatus ResolveStatus(
        LicenseStatus current,
        bool isTrial,
        DateTime? trialEndDateUtc,
        DateTime? expiryDateUtc,
        DateTime utcNow)
    {
        if (current is LicenseStatus.Suspended or LicenseStatus.Cancelled)
            return current;

        var now = EnsureUtc(utcNow);
        if (isTrial && trialEndDateUtc is DateTime trialEnd && now > EnsureUtc(trialEnd))
            return LicenseStatus.Expired;

        if (!isTrial && expiryDateUtc is DateTime expiry && now > EnsureUtc(expiry))
            return LicenseStatus.Expired;

        return current;
    }

    public static bool AllowsAccess(LicenseStatus status, bool isTrial, DateTime? trialEndDateUtc, DateTime? expiryDateUtc, DateTime utcNow)
    {
        var resolved = ResolveStatus(status, isTrial, trialEndDateUtc, expiryDateUtc, utcNow);
        return resolved is LicenseStatus.Trial or LicenseStatus.Active;
    }

    public static int GetRemainingTrialDays(DateTime trialEndDateUtc, DateTime utcNow)
    {
        var remaining = EnsureUtc(trialEndDateUtc).Date - EnsureUtc(utcNow).Date;
        return remaining.Days < 0 ? 0 : remaining.Days;
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
