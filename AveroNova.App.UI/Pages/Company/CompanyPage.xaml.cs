using AveroNova.App.UI.Layout;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.Company;

public partial class CompanyPage : ContentPage
{
    private readonly CompanyPageViewModel _vm;
    private readonly Dictionary<Grid, List<IView>> _adaptiveOrder = new();
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
            Microsoft.Maui.Controls.Grid.SetColumn(HeaderTitle, 0);
            Microsoft.Maui.Controls.Grid.SetRow(HeaderTitle, 0);
            Microsoft.Maui.Controls.Grid.SetColumn(HeaderActions, 0);
            Microsoft.Maui.Controls.Grid.SetRow(HeaderActions, 1);
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
        Microsoft.Maui.Controls.Grid.SetColumn(HeaderTitle, 0);
        Microsoft.Maui.Controls.Grid.SetRow(HeaderTitle, 0);
        Microsoft.Maui.Controls.Grid.SetColumn(HeaderActions, 1);
        Microsoft.Maui.Controls.Grid.SetRow(HeaderActions, 0);
        HeaderActions.HorizontalOptions = LayoutOptions.End;
    }

    private void ArrangeAdaptiveGrid(Grid grid, int columns)
    {
        if (grid == null)
            return;

        if (!_adaptiveOrder.TryGetValue(grid, out var children))
        {
            children = grid.Children.ToList();
            _adaptiveOrder[grid] = children;
        }

        foreach (var child in children)
        {
            Microsoft.Maui.Controls.Grid.SetColumn(child, 0);
            Microsoft.Maui.Controls.Grid.SetRow(child, 0);
        }

        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        for (var column = 0; column < columns; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var rowCount = (int)Math.Ceiling(children.Count / (double)columns);
        for (var row = 0; row < rowCount; row++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var index = 0; index < children.Count; index++)
        {
            Microsoft.Maui.Controls.Grid.SetColumn(children[index], index % columns);
            Microsoft.Maui.Controls.Grid.SetRow(children[index], index / columns);
        }
    }
}
