namespace AveroNova.App.UI.SubscriptionAccess;

internal static class TrialReminderState
{
    private static readonly HashSet<string> SessionShown = [];

    public static bool TryBeginShow(Guid userId, Guid companyId, DateTime endDate)
    {
        var key = Key(userId, companyId, endDate);
        lock (SessionShown)
        {
            if (SessionShown.Contains(key))
                return false;

            if (Preferences.Default.Get(PreferenceKey(key), false))
                return false;

            SessionShown.Add(key);
            Preferences.Default.Set(PreferenceKey(key), true);
            return true;
        }
    }

    public static void ClearSession()
    {
        lock (SessionShown)
            SessionShown.Clear();
    }

    private static string Key(Guid userId, Guid companyId, DateTime endDate)
        => $"{userId:N}:{companyId:N}:{endDate.Date:yyyyMMdd}";

    private static string PreferenceKey(string key)
        => "averonova.trial.reminder." + key;
}
