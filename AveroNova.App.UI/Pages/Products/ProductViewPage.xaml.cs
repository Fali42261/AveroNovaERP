using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.Products;

[QueryProperty(nameof(ProductId), "id")]
public partial class ProductViewPage : ContentPage
{
    private readonly ProductViewViewModel _vm;

    public string? ProductId { get; set; }

    public ProductViewPage(ProductViewViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        DetailsView.BindingContext = vm;
        if (Content != null)
            Content.BindingContext = vm;

        _vm.BackRequested += async (_, _) => await GoBackAsync();
        _vm.EditRequested += async (_, id) =>
            await Shell.Current.GoToAsync($"{AppRoutes.ProductEdit}?id={id}");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Guid.TryParse(ProductId, out var id) && id != Guid.Empty)
            await _vm.LoadAsync(id);
    }

    private static Task GoBackAsync()
        => Shell.Current.GoToAsync("..");
}
