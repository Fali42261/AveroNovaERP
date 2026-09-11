using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Products;

[QueryProperty(nameof(EditId), "id")]
public partial class ProductFormPage : ContentPage, IHostedPage
{
    private readonly IProductService _svc;
    private readonly ICompanyService _company;
    private readonly IMainContentNavigator _navigator;
    private ProductModel? _editing;
    private bool _loaded;
    private bool _saving;
    public string? EditId { get; set; }

    public ProductFormPage(IProductService svc, ICompanyService company, IMainContentNavigator navigator)
    {
        InitializeComponent();
        _svc = svc;
        _company = company;
        _navigator = navigator;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }

    public async Task LoadForHostAsync()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            if (!string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var id))
            {
                _editing = await _svc.GetByIdAsync(id);
                if (_editing != null)
                {
                    LblTitle.Text = "Edit Product";
                    EntryName.Text = _editing.Name;
                    EntrySku.Text = _editing.SKU;
                    EntryBarcode.Text = _editing.Barcode;
                    EntryCategory.Text = _editing.Category;
                    EntryBrand.Text = _editing.Brand;
                    EntryUnit.Text = _editing.Unit;
                    EditorDesc.Text = _editing.Description;
                    EntryPurchasePrice.Text = _editing.PurchasePrice.ToString("N2");
                    EntrySellingPrice.Text = _editing.SellingPrice.ToString("N2");
                    EntryTax.Text = _editing.TaxPercent.ToString("N0");
                    EntryStock.Text = _editing.Stock.ToString();
                    EntryMinStock.Text = _editing.MinimumStock.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProductForm] Load failed: {ex}");
            ShowError("Product could not be loaded.");
        }
    }

    private async void OnSaveClicked(object? s, EventArgs e)
    {
        if (_saving) return;
        ErrorBanner.IsVisible = false;

        if (string.IsNullOrWhiteSpace(EntryName.Text))
        {
            ShowError("Product name is required.");
            return;
        }
        if (!decimal.TryParse(EntrySellingPrice.Text, out var sp) || sp < 0)
        {
            ShowError("Enter a valid selling price.");
            return;
        }

        var companyId = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        if (companyId == Guid.Empty)
        {
            ShowError("No company is selected.");
            return;
        }

        _saving = true;
        try
        {
            var model = _editing ?? new ProductModel { CompanyId = companyId };
            model.CompanyId = companyId;
            model.Name = EntryName.Text.Trim();
            model.SKU = EntrySku.Text?.Trim() ?? string.Empty;
            model.Barcode = EntryBarcode.Text?.Trim() ?? string.Empty;
            model.Category = EntryCategory.Text?.Trim() ?? string.Empty;
            model.Brand = EntryBrand.Text?.Trim() ?? string.Empty;
            model.Unit = EntryUnit.Text?.Trim() is { Length: > 0 } u ? u : "pcs";
            model.Description = EditorDesc.Text?.Trim() ?? string.Empty;
            model.SellingPrice = sp;
            model.PurchasePrice = decimal.TryParse(EntryPurchasePrice.Text, out var pp) ? pp : 0m;
            model.TaxPercent = decimal.TryParse(EntryTax.Text, out var t) ? t : 0m;
            model.Stock = int.TryParse(EntryStock.Text, out var st) ? Math.Max(0, st) : 0;
            model.MinimumStock = int.TryParse(EntryMinStock.Text, out var ms) ? Math.Max(0, ms) : 0;

            var (ok, err) = _editing == null
                ? await _svc.CreateAsync(model)
                : await _svc.UpdateAsync(model);

            if (!ok)
            {
                ShowError(err ?? "Save failed.");
                return;
            }

            await _navigator.GoBackAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProductForm] Save failed: {ex}");
            ShowError("Product could not be saved. Please try again.");
        }
        finally
        {
            _saving = false;
        }
    }

    private async void OnBackClicked(object? s, EventArgs e)
    {
        try { await _navigator.GoBackAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ProductForm] Back failed: {ex}"); }
    }

    private void ShowError(string msg)
    {
        LblError.Text = msg;
        ErrorBanner.IsVisible = true;
    }
}
