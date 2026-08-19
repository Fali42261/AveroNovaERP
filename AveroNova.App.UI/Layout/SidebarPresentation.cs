using AveroNova.Application.Navigation;

namespace AveroNova.App.UI.Layout;

public enum SidebarDensity
{
    Desktop,
    Tablet,
    Mobile
}

/// <summary>
/// Presentation-only grouping for the ERP sidebar. Does not change permission
/// or subscription rules; it only labels the dynamic menu nodes already returned
/// by the access layer.
/// </summary>
internal static class SidebarSectionMap
{
    public static string? ForKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        if (key.Equals(NavigationMenuCatalog.Dashboard, StringComparison.OrdinalIgnoreCase))
            return "DASHBOARD";

        if (key.Equals(NavigationMenuCatalog.Business, StringComparison.OrdinalIgnoreCase)
            || IsBusiness(key))
            return "BUSINESS";

        if (key.Equals(NavigationMenuCatalog.Reports, StringComparison.OrdinalIgnoreCase))
            return "REPORTS";

        return null;
    }

    private static bool IsBusiness(string key)
        => key.Equals(NavigationMenuCatalog.Company, StringComparison.OrdinalIgnoreCase)
           || key.Equals(NavigationMenuCatalog.Customers, StringComparison.OrdinalIgnoreCase)
           || key.Equals(NavigationMenuCatalog.Products, StringComparison.OrdinalIgnoreCase)
           || key.Equals(NavigationMenuCatalog.Inventory, StringComparison.OrdinalIgnoreCase)
           || key.Equals(NavigationMenuCatalog.Sales, StringComparison.OrdinalIgnoreCase)
           || key.Equals(NavigationMenuCatalog.Purchase, StringComparison.OrdinalIgnoreCase)
           || key.Equals(NavigationMenuCatalog.Payments, StringComparison.OrdinalIgnoreCase);
}

internal static class SidebarTokens
{
    public static double Number(string key, double fallback)
    {
        var resources = Microsoft.Maui.Controls.Application.Current?.Resources;
        if (resources != null && resources.TryGetValue(key, out var value))
        {
            return value switch
            {
                double d => d,
                float f => f,
                int i => i,
                _ => fallback
            };
        }

        return fallback;
    }

    public static Color Color(string lightKey, string darkKey)
    {
        var resources = Microsoft.Maui.Controls.Application.Current?.Resources;
        if (resources == null)
            return Colors.Gray;

        var dark = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark
                   || Microsoft.Maui.Controls.Application.Current?.UserAppTheme == AppTheme.Dark;
        var key = dark ? darkKey : lightKey;
        if (resources.TryGetValue(key, out var value) && value is Color color)
            return color;
        return Colors.Gray;
    }

    public static Style? Style(string key)
    {
        var resources = Microsoft.Maui.Controls.Application.Current?.Resources;
        if (resources != null && resources.TryGetValue(key, out var value) && value is Style style)
            return value as Style ?? style;
        return null;
    }

    public static string Glyph(string resourceKey, string fallback = "•")
    {
        var resources = Microsoft.Maui.Controls.Application.Current?.Resources;
        if (resources != null
            && resources.TryGetValue(resourceKey, out var value)
            && value is string text
            && !string.IsNullOrWhiteSpace(text)
            && !text.Any(char.IsSurrogate))
            return text;
        return fallback;
    }
}
