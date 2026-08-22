using AveroNova.App.UI.Layout;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Views.Products;

public partial class ProductDetailsView : ContentView
{
    private int _appliedColumns;

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

    private void OnRootSizeChanged(object? sender, EventArgs e)
        => ApplyResponsiveLayout();

    private void ApplyResponsiveLayout()
    {
        if (Root.Width <= 0)
            return;

        var size = ResponsiveBreakpoints.FromWidth(Root.Width);
        var columns = size switch
        {
            ScreenSize.Compact => 1,
            ScreenSize.Medium => 2,
            _ => 3
        };
        if (columns == _appliedColumns)
            return;

        _appliedColumns = columns;
        if (BindingContext is ProductViewViewModel vm)
            vm.CardColumns = columns;

        var columnDefs = new ColumnDefinitionCollection();
        for (var i = 0; i < columns; i++)
            columnDefs.Add(new ColumnDefinition(GridLength.Star));
        CardsGrid.ColumnDefinitions = columnDefs;

        var cards = new View[] { ProductCard, PricingCard, InventoryCard };
        var rowsNeeded = Math.Max(1, (int)Math.Ceiling(cards.Length / (double)columns));
        var rowDefs = new RowDefinitionCollection();
        for (var i = 0; i < rowsNeeded; i++)
            rowDefs.Add(new RowDefinition(GridLength.Auto));
        CardsGrid.RowDefinitions = rowDefs;
        CardsGrid.ColumnSpacing = columns == 1 ? 0 : 16;

        for (var i = 0; i < cards.Length; i++)
        {
            Grid.SetColumn(cards[i], i % columns);
            Grid.SetRow(cards[i], i / columns);
        }
    }
}
