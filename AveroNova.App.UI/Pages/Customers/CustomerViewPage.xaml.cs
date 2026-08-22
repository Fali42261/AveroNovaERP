using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.Customers;

[QueryProperty(nameof(CustomerId), "id")]
public partial class CustomerViewPage : ContentPage
{
    private readonly CustomerViewViewModel _vm;

    public string? CustomerId { get; set; }

    public CustomerViewPage(CustomerViewViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        DetailsView.BindingContext = vm;
        if (Content != null)
            Content.BindingContext = vm;

        SizeChanged += OnSizeChanged;
        _vm.BackRequested += async (_, _) => await GoBackAsync();
        _vm.Deleted += async (_, _) => await GoBackAsync();
        _vm.EditRequested += async (_, id) =>
            await Shell.Current.GoToAsync($"{AppRoutes.CustomerEdit}?id={id}");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Guid.TryParse(CustomerId, out var id) && id != Guid.Empty)
            await _vm.LoadAsync(id);
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0)
            return;
        _vm.IsInlineLayout = ResponsiveBreakpoints.FromWidth(Width) != ScreenSize.Compact;
    }

    private static Task GoBackAsync()
        => Shell.Current.GoToAsync("..");
}
