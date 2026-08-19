namespace AveroNova.App.UI.Navigation;

/// <summary>
/// Lets hosted pages (Dashboard) switch MainLayout modules without duplicating routes.
/// Form pages still use <see cref="AppRoutes"/> + Shell.
/// </summary>
public static class MainContentNavigator
{
    public const string Billing = "billing";
    public const string Customers = "customers";
    public const string Products = "products";
    public const string Payments = "payments";
    public const string Inventory = "inventory";

    public static event Action<string>? NavigateRequested;

    public static void Request(string destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
            return;
        NavigateRequested?.Invoke(destination);
    }
}
