namespace AveroNova.Application.Navigation;

/// <summary>
/// Where a catalog item is presented. Does not change permission or subscription rules.
/// </summary>
public enum NavigationSurface
{
    Sidebar = 0,
    HeaderAccount = 1
}

public sealed class NavigationMenuDefinition
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required string IconResourceKey { get; init; }

    public required string SubscriptionModule { get; init; }

    public required string PermissionName { get; init; }

    public required int SortOrder { get; init; }

    public string? ParentKey { get; init; }

    public bool IsGroup { get; init; }

    /// <summary>
    /// Visual section heading only. Does not create a parent/child menu relationship.
    /// Hierarchy is ParentKey.
    /// </summary>
    public string? GroupLabel { get; init; }

    /// <summary>
    /// Presentation only. HeaderAccount items remain in the catalog for
    /// permission checks but are not shown as Sidebar navigation.
    /// </summary>
    public NavigationSurface Surface { get; init; } = NavigationSurface.Sidebar;
}

public sealed class NavigationMenuNode
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required string IconResourceKey { get; init; }

    public required string SubscriptionModule { get; init; }

    public required string PermissionName { get; init; }

    public required int SortOrder { get; init; }

    public bool IsGroup { get; init; }

    public string? GroupLabel { get; init; }

    public NavigationSurface Surface { get; init; } = NavigationSurface.Sidebar;

    public IReadOnlyList<NavigationMenuNode> Children { get; init; } = [];

    public bool IsAccordion => IsGroup && Children.Count > 0;
}
