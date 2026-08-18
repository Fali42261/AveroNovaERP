namespace AveroNova.App.UI.Services.Local;

/// <summary>
/// Persists the active local session. Auth restore reads this first,
/// then verifies the user still exists in SQLite. No API call is required.
/// </summary>
internal static class LocalSessionStore
{
    public const string UserIdKey = "averonova.session.userId";
    public const string CompanyIdKey = "averonova.session.companyId";
    public const string EmailKey = "averonova.session.email";
    public const string HasLocalAccountKey = "averonova.has_local_account";

    public static Guid? UserId => ParseGuid(Preferences.Default.Get(UserIdKey, string.Empty));
    public static Guid? CompanyId => ParseGuid(Preferences.Default.Get(CompanyIdKey, string.Empty));
    public static string Email => Preferences.Default.Get(EmailKey, string.Empty);

    public static void Set(Guid userId, Guid companyId, string email)
    {
        Preferences.Default.Set(UserIdKey, userId.ToString());
        Preferences.Default.Set(CompanyIdKey, companyId.ToString());
        Preferences.Default.Set(EmailKey, email);
        Preferences.Default.Set(HasLocalAccountKey, true);
    }

    public static void ClearSession()
    {
        Preferences.Default.Remove(UserIdKey);
        Preferences.Default.Remove(CompanyIdKey);
        Preferences.Default.Remove(EmailKey);
    }

    public static void MarkLocalAccountExists()
        => Preferences.Default.Set(HasLocalAccountKey, true);

    private static Guid? ParseGuid(string? value)
        => Guid.TryParse(value, out var id) ? id : null;
}
