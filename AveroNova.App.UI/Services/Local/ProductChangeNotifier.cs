namespace AveroNova.App.UI.Services.Local;

internal static class ProductChangeNotifier
{
    public static event EventHandler? Changed;

    public static void Notify() => Changed?.Invoke(null, EventArgs.Empty);
}
