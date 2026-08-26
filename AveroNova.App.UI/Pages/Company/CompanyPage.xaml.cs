using AveroNova.App.UI.Layout;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.Company;

public partial class CompanyPage : ContentPage
{
    private readonly CompanyPageViewModel _vm;

    public CompanyPage(CompanyPageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        if (Content != null)
            Content.BindingContext = vm;
        ErrorState.RetryClicked += async (_, _) => await _vm.LoadAsync();
        SizeChanged += (_, _) => ApplyResponsiveLayout();
        Loaded += (_, _) => ApplyResponsiveLayout();
    }

    public Task ReloadAsync() => _vm.LoadAsync();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ApplyResponsiveLayout();
        await _vm.LoadAsync();
    }

    private void ApplyResponsiveLayout()
    {
        if (Width <= 0) return;
        var size = ResponsiveBreakpoints.FromWidth(Width);
        var columns = size == ScreenSize.Compact ? 1 : size == ScreenSize.Medium ? 2 : 3;
        ApplyGrid(ViewFieldsGrid, columns);
        ApplyGrid(ViewAddressGrid, columns);
        ApplyGrid(EditFieldsGrid, columns);
        ApplyGrid(EditAddressGrid, columns);
    }

    private static void ApplyGrid(Grid grid, int columns)
    {
        var children = grid.Children.ToArray();
        var defs = new ColumnDefinitionCollection();
        for (var i = 0; i < columns; i++) defs.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions = defs;
        var rows = Math.Max(1, (int)Math.Ceiling(children.Length / (double)columns));
        var rowDefs = new RowDefinitionCollection();
        for (var i = 0; i < rows; i++) rowDefs.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions = rowDefs;
        for (var i = 0; i < children.Length; i++)
        {
            Grid.SetColumn((BindableObject)children[i], i % columns);
            Grid.SetRow((BindableObject)children[i], i / columns);
        }
    }
}
