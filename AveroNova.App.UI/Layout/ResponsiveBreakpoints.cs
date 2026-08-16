namespace AveroNova.App.UI.Layout;

/// <summary>
/// Single source of layout breakpoints.
/// Must stay aligned with Dimensions.xaml BreakpointMobile / BreakpointTablet.
/// Compact  &lt; 600
/// Medium   600–899
/// Expanded ≥ 900
/// </summary>
public static class ResponsiveBreakpoints
{
    public const double CompactMaxWidth = 599;
    public const double MediumMaxWidth = 899;
    public const double ExpandedMinWidth = 900;

    public static ScreenCategory FromWidth(double width)
    {
        if (width <= 0)
            return ScreenCategory.Expanded;

        if (width <= CompactMaxWidth)
            return ScreenCategory.Compact;

        if (width <= MediumMaxWidth)
            return ScreenCategory.Medium;

        return ScreenCategory.Expanded;
    }
}
