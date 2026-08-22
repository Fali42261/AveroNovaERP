using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Views.Customers;

public partial class CustomerDetailsView : ContentView
{
    private bool _isCompact;

    public CustomerDetailsView()
    {
        InitializeComponent();
        ErrorState.RetryClicked += OnRetryClicked;
    }

    private void OnRetryClicked(object? sender, EventArgs e)
    {
        if (BindingContext is CustomerViewViewModel vm)
            _ = vm.RetryCommand.ExecuteAsync(null);
    }

    private void OnHeaderSizeChanged(object? sender, EventArgs e)
    {
        var width = HeaderBar.Width > 1 ? HeaderBar.Width : Width;
        if (width <= 0)
            return;

        var compact = width < 520;
        if (compact == _isCompact && HeaderBar.RowDefinitions.Count > 0)
            return;

        _isCompact = compact;
        if (compact)
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
            HeaderActions.HorizontalOptions = LayoutOptions.Start;
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
}
