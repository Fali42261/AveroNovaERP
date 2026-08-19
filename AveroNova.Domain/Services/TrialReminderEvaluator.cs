namespace AveroNova.Domain.Services
{
    public static class TrialReminderEvaluator
    {
        public static DateTime ReminderDate(DateTime endDate)
            => endDate.Date.AddDays(-1);

        public static bool IsDueTomorrow(DateTime endDate, DateTime utcNow)
            => utcNow.Date == ReminderDate(endDate);
    }
}
