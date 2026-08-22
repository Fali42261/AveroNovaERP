using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Inventory;

[QueryProperty(nameof(ProductIdParam), "productId")]
public partial class StockAdjustPage : ContentPage
{
    private readonly IInventoryService _inv;
    private readonly IProductService _product;
    private readonly ICompanyService _company;
    private List<ProductModel> _products = [];
    public string? ProductIdParam { get; set; }
    public Action? CloseRequested { get; set; }

    public StockAdjustPage(IInventoryService inv, IProductService product, ICompanyService company)
    { InitializeComponent(); _inv = inv; _product = product; _company = company; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _products = await _product.GetAllAsync(_company.CurrentCompany?.LocalId ?? Guid.Empty);
        ProductPicker.ItemsSource = _products.Select(p => p.Name).ToList();
        if (!string.IsNullOrEmpty(ProductIdParam) && Guid.TryParse(ProductIdParam, out var id))
        {
            var idx = _products.FindIndex(p => p.LocalId == id);
            if (idx >= 0) { ProductPicker.SelectedIndex = idx; UpdateCurrentStock(idx); }
        }
        ProductPicker.SelectedIndexChanged += (_, _) => UpdateCurrentStock(ProductPicker.SelectedIndex);
    }

    private void UpdateCurrentStock(int idx)
    {
        if (idx < 0 || idx >= _products.Count) return;
        LblCurrentStock.Text = _products[idx].Stock.ToString();
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (ProductPicker.SelectedIndex < 0) { ShowError("Select a product."); return; }
        if (!int.TryParse(EntryNewStock.Text, out var newStock)) { ShowError("Enter a valid stock quantity."); return; }
        if (ReasonPicker.SelectedIndex < 0) { ShowError("Select a reason."); return; }
        var product = _products[ProductPicker.SelectedIndex];
        var adj = new StockAdjustmentModel
        {
            ProductId = product.LocalId, ProductName = product.Name, CurrentStock = product.Stock,
            NewStock = newStock, Reason = ReasonPicker.SelectedItem?.ToString() ?? "",
            Notes = EditorNotes.Text?.Trim() ?? "", AdjustedBy = "Admin",
            CompanyId = _company.CurrentCompany?.LocalId ?? Guid.Empty
        };
        var (ok, err) = await _inv.AdjustStockAsync(adj);
        if (ok)
        {
            await DisplayAlert("Success", "Stock adjusted successfully.", "OK");
            CloseRequested?.Invoke();
            if (CloseRequested == null) await Shell.Current.GoToAsync("..");
        }
        else ShowError(err ?? "Adjustment failed.");
    }

    private async void OnBackClicked(object s, EventArgs e)
    {
        if (CloseRequested != null) { CloseRequested.Invoke(); return; }
        await Shell.Current.GoToAsync("..");
    }

    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
