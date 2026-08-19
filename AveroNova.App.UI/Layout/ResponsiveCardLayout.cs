namespace AveroNova.App.UI.Layout;

/// <summary>
/// Arranges equal-width dashboard cards in a Grid from available width.
/// Compact: 1 column (phones). Medium: 2 columns (tablets). Wide: 4 columns (desktop).
/// </summary>
public static class ResponsiveCardLayout
{
    public static int ColumnCount(double width)
    {
        if (width <= 0)
            return 1;
        if (width < ResponsiveBreakpoints.CompactMaxWidth)
            return 1;
        if (width < 900)
            return 2;
        return 4;
    }

    public static void Arrange(Grid grid)
    {
        if (grid.Width <= 0)
            return;

        var children = grid.Children.OfType<View>().ToList();
        if (children.Count == 0)
            return;

        var columns = ColumnCount(grid.Width);
        var rows = Math.Max(1, (int)Math.Ceiling(children.Count / (double)columns));

        if (grid.ColumnDefinitions.Count != columns)
        {
            var colDefs = new ColumnDefinitionCollection();
            for (var i = 0; i < columns; i++)
                colDefs.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions = colDefs;
        }

        if (grid.RowDefinitions.Count != rows)
        {
            var rowDefs = new RowDefinitionCollection();
            for (var i = 0; i < rows; i++)
                rowDefs.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions = rowDefs;
        }

        grid.ColumnSpacing = columns == 1 ? 0 : 12;
        grid.RowSpacing = 12;

        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            Grid.SetColumn(child, i % columns);
            Grid.SetRow(child, i / columns);
            Grid.SetColumnSpan(child, 1);
            Grid.SetRowSpan(child, 1);
            child.HorizontalOptions = LayoutOptions.Fill;
        }
    }
}
