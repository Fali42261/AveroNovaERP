namespace AveroNova.App.UI.SubscriptionAccess;

internal static class PendingAuthMessage
{
    private static string? _message;

    public static void Set(string message)
        => _message = message;

    public static string? Take()
    {
        var message = _message;
        _message = null;
        return message;
    }
}
