using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.Products;

[QueryProperty(nameof(EditId), "id")]
public partial class ProductFormPage : ContentPage
{
    private readonly ProductFormViewModel _vm;

    public string? EditId { get; set; }

    public ProductFormPage(ProductFormViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        FormView.BindingContext = vm;
        if (Content != null)
            Content.BindingContext = vm;

        _vm.Saved += async (_, _) => await GoBackAsync();
        _vm.Cancelled += async (_, _) => await GoBackAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Guid? id = null;
        if (!string.IsNullOrWhiteSpace(EditId) && Guid.TryParse(EditId, out var parsed) && parsed != Guid.Empty)
            id = parsed;
        await _vm.InitializeAsync(id);
    }

    private static Task GoBackAsync()
        => Shell.Current.GoToAsync("..");
}
