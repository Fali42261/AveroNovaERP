namespace AveroNova.App.UI.Services.Local;

internal static class UserChangeNotifier
{
    public static event EventHandler<string>? Succeeded;

    public static void Notify(string message)
        => Succeeded?.Invoke(null, message);
}
