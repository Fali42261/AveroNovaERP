namespace AveroNova.Domain.Constants;

public static class LicenseConstants
{
    public const int TrialDays = 15;
    public const string StarterPlan = PlanNames.Starter;
    public static readonly TimeSpan ClockRollbackTolerance = TimeSpan.FromMinutes(5);
}
