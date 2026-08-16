using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Products;

[QueryProperty(nameof(EditId), "id")]
public partial class ProductFormPage : ContentPage
{
    private readonly IProductService _svc;
    private readonly ICompanyService _company;
    private ProductModel? _editing;
    public string? EditId { get; set; }

    public ProductFormPage(IProductService svc, ICompanyService company) { InitializeComponent(); _svc = svc; _company = company; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var id))
        {
            _editing = await _svc.GetByIdAsync(id);
            if (_editing != null)
            {
                LblTitle.Text           = "Edit Product";
                EntryName.Text          = _editing.Name;
                EntrySku.Text           = _editing.SKU;
                EntryBarcode.Text       = _editing.Barcode;
                EntryCategory.Text      = _editing.Category;
                EntryBrand.Text         = _editing.Brand;
                EntryUnit.Text          = _editing.Unit;
                EditorDesc.Text         = _editing.Description;
                EntryPurchasePrice.Text = _editing.PurchasePrice.ToString("N2");
                EntrySellingPrice.Text  = _editing.SellingPrice.ToString("N2");
                EntryTax.Text           = _editing.TaxPercent.ToString("N0");
                EntryStock.Text         = _editing.Stock.ToString();
                EntryMinStock.Text      = _editing.MinimumStock.ToString();
            }
        }
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryName.Text)) { ShowError("Product name is required."); return; }
        if (!decimal.TryParse(EntrySellingPrice.Text, out var sp)) { ShowError("Enter a valid selling price."); return; }

        var model           = _editing ?? new ProductModel { CompanyId = _company.CurrentCompany?.LocalId ?? Guid.Empty };
        model.Name          = EntryName.Text.Trim();
        model.SKU           = EntrySku.Text?.Trim() ?? "";
        model.Barcode       = EntryBarcode.Text?.Trim() ?? "";
        model.Category      = EntryCategory.Text?.Trim() ?? "";
        model.Brand         = EntryBrand.Text?.Trim() ?? "";
        model.Unit          = EntryUnit.Text?.Trim() is { Length: > 0 } u ? u : "pcs";
        model.Description   = EditorDesc.Text?.Trim() ?? "";
        model.SellingPrice  = sp;
        model.PurchasePrice = decimal.TryParse(EntryPurchasePrice.Text, out var pp) ? pp : 0m;
        model.TaxPercent    = decimal.TryParse(EntryTax.Text, out var t) ? t : 0m;
        model.Stock         = int.TryParse(EntryStock.Text, out var st) ? st : 0;
        model.MinimumStock  = int.TryParse(EntryMinStock.Text, out var ms) ? ms : 0;

        var (ok, err) = _editing == null ? await _svc.CreateAsync(model) : await _svc.UpdateAsync(model);
        if (ok) await Shell.Current.GoToAsync("..");
        else ShowError(err ?? "Save failed.");
    }

    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
