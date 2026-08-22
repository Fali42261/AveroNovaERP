namespace AveroNova.App.UI.Layout;

public enum ScreenSize
{
    Compact = 0,
    Medium = 1,
    Expanded = 2
}

/// <summary>
/// Width-based breakpoints for the AveroNova responsive design system.
/// Compact  &lt; 700 — phones, stacked 1-column
/// Medium   700–1099 — tablets / small windows, 2 columns
/// Expanded ≥ 1100 — desktop, up to 3 columns for forms
/// </summary>
public static class ResponsiveBreakpoints
{
    public const double CompactMaxWidth = 699;
    public const double MediumMaxWidth = 1099;
    public const double ShellDesktopMinWidth = 900;
    public const double MinFieldWidth = 300;
    public const double FormMaxCompact = 480;
    public const double FormMaxMedium = 760;
    public const double FormMaxExpanded = 1120;
    public const double PageGutter = 48;
    public const double SidebarDesktopWidth = 256;
    public const double SidebarTabletWidth = 240;
    public const double SidebarMobileDrawerWidth = 280;
    public const double SidebarCollapsedWidth = 64;

    public static double DockedSidebarWidth(ScreenSize size) => size switch
    {
        ScreenSize.Compact => 0,
        ScreenSize.Medium => SidebarTabletWidth,
        _ => SidebarDesktopWidth
    };

    public static ScreenSize FromWidth(double width)
    {
        if (width <= CompactMaxWidth)
            return ScreenSize.Compact;
        if (width <= MediumMaxWidth)
            return ScreenSize.Medium;
        return ScreenSize.Expanded;
    }

    public static int FormColumnCount(double availableWidth, int maxColumns = 3)
    {
        if (availableWidth < MinFieldWidth * 2)
            return 1;
        if (availableWidth < MinFieldWidth * 3)
            return Math.Min(2, maxColumns);
        return Math.Clamp(maxColumns, 1, 3);
    }

    public static double FormMaxWidth(ScreenSize size) => size switch
    {
        ScreenSize.Compact => FormMaxCompact,
        ScreenSize.Medium => FormMaxMedium,
        _ => FormMaxExpanded
    };
}
