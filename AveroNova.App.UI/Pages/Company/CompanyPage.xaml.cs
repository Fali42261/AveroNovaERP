using AveroNova.App.UI.Layout;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.Company;

public partial class CompanyPage : ContentPage
{
    private readonly CompanyPageViewModel _vm;
    private readonly Dictionary<Grid, List<View>> _adaptiveOrder = new();
    private int _appliedViewColumns;
    private int _appliedFormColumns;

    public CompanyPage(CompanyPageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        if (Content != null)
            Content.BindingContext = vm;
        ErrorState.RetryClicked += async (_, _) => await _vm.LoadAsync();
    }

    public Task ReloadAsync() => _vm.LoadAsync();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ApplyResponsiveLayout();
        await _vm.LoadAsync();
    }

    private void OnRootSizeChanged(object? sender, EventArgs e)
        => ApplyResponsiveLayout();

    private void ApplyResponsiveLayout()
    {
        if (Root.Width <= 0)
            return;

        var size = ResponsiveBreakpoints.FromWidth(Root.Width);
        var viewColumns = size switch
        {
            ScreenSize.Compact => 1,
            ScreenSize.Medium => 2,
            _ => 3
        };
        var formColumns = ResponsiveBreakpoints.FormColumnCount(Root.Width, maxColumns: 3);
        if (viewColumns == _appliedViewColumns
            && formColumns == _appliedFormColumns
            && _adaptiveOrder.Count > 0)
            return;

        _appliedViewColumns = viewColumns;
        _appliedFormColumns = formColumns;
        ArrangeHeader(formColumns);
        ArrangeAdaptiveGrid(LoadingGrid, viewColumns);
        ArrangeAdaptiveGrid(ViewFieldsGrid, viewColumns);
        ArrangeAdaptiveGrid(EditFieldsGrid, formColumns);
        ArrangeAdaptiveGrid(EditAddressGrid, formColumns);
    }

    private void ArrangeHeader(int columns)
    {
        if (columns == 1)
        {
            HeaderBar.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star)
            };
            HeaderBar.RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            };
            Grid.SetColumn(HeaderTitle, 0);
            Grid.SetRow(HeaderTitle, 0);
            Grid.SetColumn(HeaderActions, 0);
            Grid.SetRow(HeaderActions, 1);
            HeaderActions.HorizontalOptions = LayoutOptions.End;
            return;
        }

        HeaderBar.ColumnDefinitions = new ColumnDefinitionCollection
        {
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Auto)
        };
        HeaderBar.RowDefinitions = new RowDefinitionCollection
        {
            new RowDefinition(GridLength.Auto)
        };
        Grid.SetColumn(HeaderTitle, 0);
        Grid.SetRow(HeaderTitle, 0);
        Grid.SetColumn(HeaderActions, 1);
        Grid.SetRow(HeaderActions, 0);
        HeaderActions.HorizontalOptions = LayoutOptions.End;
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

        grid.ColumnSpacing = columns == 1 ? 0 : 12;
        grid.RowSpacing = 12;
        grid.HorizontalOptions = LayoutOptions.Fill;

        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            Grid.SetColumn(child, i % columns);
            Grid.SetRow(child, i / columns);
            Grid.SetColumnSpan(child, 1);
            Grid.SetRowSpan(child, 1);
            child.HorizontalOptions = LayoutOptions.Fill;
            child.VerticalOptions = LayoutOptions.Fill;
            child.MinimumWidthRequest = 0;
        }
    }
}
