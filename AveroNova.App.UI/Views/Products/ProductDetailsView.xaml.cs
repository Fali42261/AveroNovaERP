using AveroNova.App.UI.Layout;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Views.Products;

public partial class ProductDetailsView : ContentView
{
    public ProductDetailsView()
    {
        InitializeComponent();
        ErrorState.RetryClicked += OnRetryClicked;
        NotFoundState.ActionClicked += OnNotFoundBackClicked;
        Loaded += (_, _) => ApplyResponsiveLayout();
    }

    private void OnRetryClicked(object? sender, EventArgs e)
    {
        if (BindingContext is ProductViewViewModel vm)
            _ = vm.RetryCommand.ExecuteAsync(null);
    }

    private void OnNotFoundBackClicked(object? sender, EventArgs e)
    {
        if (BindingContext is ProductViewViewModel vm)
            vm.BackCommand.Execute(null);
    }

    private void OnRootSizeChanged(object? sender, EventArgs e) => ApplyResponsiveLayout();

    private void ApplyResponsiveLayout()
    {
        if (Root.Width <= 0) return;
        var size = ResponsiveBreakpoints.FromWidth(Root.Width);
        var columns = size == ScreenSize.Compact ? 1 : size == ScreenSize.Medium ? 2 : 3;
        ApplyGrid(ProductFieldsGrid, columns);
        ApplyGrid(PricingFieldsGrid, columns);
        ApplyGrid(InventoryFieldsGrid, columns);
        if (BindingContext is ProductViewViewModel vm) vm.CardColumns = columns;
    }

    private static void ApplyGrid(Grid grid, int columns)
    {
        var children = grid.Children.ToArray();
        var defs = new ColumnDefinitionCollection();
        for (var i = 0; i < columns; i++) defs.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions = defs;
        var rowDefs = new RowDefinitionCollection();
        var rows = Math.Max(1, (int)Math.Ceiling(children.Length / (double)columns));
        for (var i = 0; i < rows; i++) rowDefs.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions = rowDefs;
        for (var i = 0; i < children.Length; i++)
        {
            Grid.SetColumn((BindableObject)children[i], i % columns);
            Grid.SetRow((BindableObject)children[i], i / columns);
        }
    }
}
