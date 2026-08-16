using System.Runtime.CompilerServices;
using Microsoft.Maui;

namespace AveroNova.App.UI.Layout;

/// <summary>
/// Globally adapts equal-star field grids to 1/2/3 columns from available width.
/// Grids with Auto/absolute columns (headers, toolbars, sidebars) are left alone.
/// </summary>
public static class ResponsivePage
{
    public static readonly BindableProperty IgnoreProperty =
        BindableProperty.CreateAttached("Ignore", typeof(bool), typeof(ResponsivePage), false);

    public static bool GetIgnore(BindableObject view) => (bool)view.GetValue(IgnoreProperty);
    public static void SetIgnore(BindableObject view, bool value) => view.SetValue(IgnoreProperty, value);

    private static readonly ConditionalWeakTable<Element, Tracker> Trackers = new();

    public static void Attach(Page page)
    {
        Trackers.GetValue(page, static key => new Tracker((Page)key));
    }

    private sealed class Tracker
    {
        private readonly Page _page;
        private readonly Dictionary<Grid, GridSnapshot> _snapshots = new();
        private double _lastWidth;

        public Tracker(Page page)
        {
            _page = page;
            _page.SizeChanged += (_, _) => Apply();
            _page.Appearing += (_, _) => Apply();
        }

        private void Apply()
        {
            var width = _page.Width;
            if (width <= 0 || Math.Abs(width - _lastWidth) < 1)
            {
                if (width > 0 && _snapshots.Count == 0)
                    ApplyCore(width);
                return;
            }

            _lastWidth = width;
            ApplyCore(width);
        }

        private void ApplyCore(double width)
        {
            var size = ResponsiveBreakpoints.FromWidth(width);
            Walk(_page, size);
        }

        private void Walk(Element element, ScreenSize size)
        {
            if (element is Grid grid)
                Adapt(grid, size);

            if (element is IVisualTreeElement visual)
            {
                foreach (var child in visual.GetVisualChildren())
                {
                    if (child is Element childElement)
                        Walk(childElement, size);
                }
            }
        }

        private void Adapt(Grid grid, ScreenSize size)
        {
            if (GetIgnore(grid))
                return;

            if (!_snapshots.TryGetValue(grid, out var snapshot))
            {
                if (!IsAdaptiveFieldGrid(grid))
                    return;

                snapshot = Capture(grid);
                _snapshots[grid] = snapshot;
            }

            var columns = size switch
            {
                ScreenSize.Compact => 1,
                ScreenSize.Medium => Math.Min(2, snapshot.OriginalColumns),
                _ => snapshot.OriginalColumns
            };

            if (columns == snapshot.OriginalColumns)
            {
                Restore(grid, snapshot);
                return;
            }

            var defs = new ColumnDefinitionCollection();
            for (var i = 0; i < columns; i++)
                defs.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions = defs;

            var rowsNeeded = Math.Max(1, (int)Math.Ceiling(snapshot.Children.Count / (double)columns));
            var rows = new RowDefinitionCollection();
            for (var i = 0; i < rowsNeeded; i++)
                rows.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions = rows;

            for (var i = 0; i < snapshot.Children.Count; i++)
            {
                var child = snapshot.Children[i];
                Grid.SetColumn(child.View, i % columns);
                Grid.SetRow(child.View, i / columns);
                Grid.SetColumnSpan(child.View, 1);
                Grid.SetRowSpan(child.View, 1);
            }
        }

        private static void Restore(Grid grid, GridSnapshot snapshot)
        {
            grid.ColumnDefinitions = snapshot.OriginalColumnsDefs;
            grid.RowDefinitions = snapshot.OriginalRowDefs;
            foreach (var child in snapshot.Children)
            {
                Grid.SetColumn(child.View, child.Column);
                Grid.SetRow(child.View, child.Row);
                Grid.SetColumnSpan(child.View, child.ColumnSpan);
                Grid.SetRowSpan(child.View, child.RowSpan);
            }
        }

        private static bool IsAdaptiveFieldGrid(Grid grid)
        {
            var count = grid.ColumnDefinitions.Count;
            if (count is < 2 or > 4)
                return false;

            foreach (var column in grid.ColumnDefinitions)
            {
                if (!column.Width.IsStar)
                    return false;
            }

            foreach (var child in grid.Children.OfType<View>())
            {
                if (Grid.GetColumnSpan(child) > 1 || Grid.GetRowSpan(child) > 1)
                    return false;
            }

            return grid.Children.OfType<View>().Any();
        }

        private static GridSnapshot Capture(Grid grid)
        {
            var children = grid.Children
                .OfType<View>()
                .OrderBy(v => Grid.GetRow(v))
                .ThenBy(v => Grid.GetColumn(v))
                .Select(v => new ChildPlacement(
                    v,
                    Grid.GetColumn(v),
                    Grid.GetRow(v),
                    Grid.GetColumnSpan(v),
                    Grid.GetRowSpan(v)))
                .ToList();

            return new GridSnapshot(
                grid.ColumnDefinitions.Count,
                CloneColumns(grid.ColumnDefinitions),
                CloneRows(grid.RowDefinitions),
                children);
        }

        private static ColumnDefinitionCollection CloneColumns(ColumnDefinitionCollection source)
        {
            var copy = new ColumnDefinitionCollection();
            foreach (var column in source)
                copy.Add(new ColumnDefinition(column.Width));
            return copy;
        }

        private static RowDefinitionCollection CloneRows(RowDefinitionCollection source)
        {
            var copy = new RowDefinitionCollection();
            foreach (var row in source)
                copy.Add(new RowDefinition(row.Height));
            return copy;
        }
    }

    private sealed record ChildPlacement(View View, int Column, int Row, int ColumnSpan, int RowSpan);

    private sealed record GridSnapshot(
        int OriginalColumns,
        ColumnDefinitionCollection OriginalColumnsDefs,
        RowDefinitionCollection OriginalRowDefs,
        List<ChildPlacement> Children);
}
