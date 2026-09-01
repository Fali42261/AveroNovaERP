using AveroNova.App.UI.Layout;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Views.Customers;

public partial class CustomerDetailsView : ContentView
{
    private int _appliedColumns;

    public CustomerDetailsView()
    {
        InitializeComponent();
        ErrorState.RetryClicked += OnRetryClicked;
        Loaded += (_, _) => ApplyResponsiveLayout();
    }

    private void OnRetryClicked(object? sender, EventArgs e)
    {
        if (BindingContext is CustomerViewViewModel vm)
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
            : 3;

        if (columns == _appliedColumns)
            return;

        _appliedColumns = columns;
        ApplyGridColumns(CustomerFieldsGrid, columns, 6);
        ApplyGridColumns(AddressFieldsGrid, columns, 5);
    }

    private static void ApplyGridColumns(Grid grid, int columns, int childCount)
    {
        var columnDefs = new ColumnDefinitionCollection();
        for (var i = 0; i < columns; i++)
            columnDefs.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions = columnDefs;

        var rowsNeeded = Math.Max(1, (int)Math.Ceiling(childCount / (double)columns));
        var rowDefs = new RowDefinitionCollection();
        for (var i = 0; i < rowsNeeded; i++)
            rowDefs.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions = rowDefs;

        grid.ColumnSpacing = columns == 1 ? 0 : 16;
        grid.RowSpacing = 16;

        var children = grid.Children.OfType<View>().ToList();
        for (var i = 0; i < children.Count; i++)
        {
            Grid.SetColumn(children[i], i % columns);
            Grid.SetRow(children[i], i / columns);
        }
    }
}
