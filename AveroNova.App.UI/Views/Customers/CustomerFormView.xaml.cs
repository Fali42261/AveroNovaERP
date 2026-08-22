using AveroNova.App.UI.Layout;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Views.Customers;

public partial class CustomerFormView : ContentView
{
    private readonly Dictionary<Grid, List<View>> _adaptiveOrder = new();
    private int _appliedColumns;

    public CustomerFormView()
    {
        InitializeComponent();
        ErrorState.RetryClicked += OnRetryClicked;
        Loaded += (_, _) => ApplyResponsiveLayout();
    }

    private void OnRetryClicked(object? sender, EventArgs e)
    {
        if (BindingContext is CustomerFormViewModel vm)
            _ = vm.RetryCommand.ExecuteAsync(null);
    }

    private void OnRootSizeChanged(object? sender, EventArgs e)
        => ApplyResponsiveLayout();

    private void ApplyResponsiveLayout()
    {
        if (Root.Width <= 0)
            return;

        var columns = ResponsiveBreakpoints.FromWidth(Root.Width) == ScreenSize.Compact
            ? 1
            : ResponsiveBreakpoints.FormColumnCount(Root.Width, maxColumns: 2);
        if (columns == _appliedColumns && _adaptiveOrder.Count > 0)
            return;

        _appliedColumns = columns;
        ArrangeAdaptiveGrid(DetailsGrid, columns);
        ArrangeAdaptiveGrid(AddressGrid, columns);
        ArrangeAdaptiveGrid(AdditionalGrid, columns);
    }

    private void ArrangeAdaptiveGrid(Grid grid, int columns)
    {
        if (!_adaptiveOrder.TryGetValue(grid, out var children))
        {
            children = grid.Children
                .OfType<View>()
                .OrderBy(Grid.GetRow)
                .ThenBy(Grid.GetColumn)
                .ToList();
            _adaptiveOrder[grid] = children;
        }

        if (children.Count == 0)
            return;

        var columnDefs = new ColumnDefinitionCollection();
        for (var i = 0; i < columns; i++)
            columnDefs.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions = columnDefs;

        var rowsNeeded = Math.Max(1, (int)Math.Ceiling(children.Count / (double)columns));
        var rowDefs = new RowDefinitionCollection();
        for (var i = 0; i < rowsNeeded; i++)
            rowDefs.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions = rowDefs;

        grid.ColumnSpacing = columns == 1 ? 0 : 16;
        grid.RowSpacing = 14;
        grid.HorizontalOptions = LayoutOptions.Fill;

        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            Grid.SetColumn(child, i % columns);
            Grid.SetRow(child, i / columns);
            Grid.SetColumnSpan(child, 1);
            Grid.SetRowSpan(child, 1);
            child.HorizontalOptions = LayoutOptions.Fill;
            child.MinimumWidthRequest = 0;
        }
    }
}
