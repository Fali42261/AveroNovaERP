using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Navigation;

namespace AveroNova.App.UI.Pages.Purchases;

[QueryProperty(nameof(EditId), "id")]
public partial class PurchaseFormPage : ContentPage, IHostedPage
{
    private readonly IPurchaseService _svc;
    private readonly ICompanyService  _company;
    private readonly ISupplierService _suppliers;
    private readonly IProductService _productsService;
    private readonly IMainContentNavigator _navigator;
    private List<SupplierModel> _supplierItems = [];
    private List<ProductModel> _productItems = [];
    private PurchaseModel? _editing;
    public string? EditId { get; set; }

    public PurchaseFormPage(IPurchaseService svc, ICompanyService company, ISupplierService suppliers, IProductService productsService, IMainContentNavigator navigator)
    { InitializeComponent(); _svc = svc; _company = company; _suppliers = suppliers; _productsService = productsService; _navigator = navigator; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }

    public async Task LoadForHostAsync()
    {
        ErrorBanner.IsVisible = false;
        var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        _supplierItems = await _suppliers.GetAllAsync(cid);
        _productItems = await _productsService.GetAllAsync(cid);
        SupplierPicker.ItemsSource = _supplierItems.Select(x => x.Name).ToList();
        ProductPicker.ItemsSource = _productItems.Select(x => $"{x.Name} ({x.SKU})").ToList();
        if (_editing is null) { DatePurchase.Date = DateTime.Today; DateDue.Date = DateTime.Today.AddDays(30); PaymentPicker.SelectedIndex = 0; StatusPicker.SelectedIndex = 0; }
        if (!string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var id))
        {
            _editing = await _svc.GetByIdAsync(id);
            if (_editing != null)
            {
                LblTitle.Text       = "Edit Purchase";
                SupplierPicker.SelectedIndex = _supplierItems.FindIndex(x => x.LocalId == _editing.SupplierId);
                DatePurchase.Date   = _editing.PurchaseDate;
                DateDue.Date        = _editing.DueDate;
                EntryRef.Text       = _editing.Reference;
                EditorNotes.Text    = _editing.Notes;
                PaymentPicker.SelectedIndex = (int)_editing.PaymentMethod;
                StatusPicker.SelectedIndex = (int)_editing.Status;
                EntryPaid.Text = _editing.PaidAmount.ToString("0.##");
                var line = _editing.Items.FirstOrDefault();
                if (line is not null) { ProductPicker.SelectedIndex = _productItems.FindIndex(x => x.LocalId == line.ProductId); EntryQuantity.Text=line.Quantity.ToString(); EntryUnitPrice.Text=line.UnitPrice.ToString("0.##"); EntryTax.Text=line.TaxPct.ToString("0.##"); }
            }
        }
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (SupplierPicker.SelectedIndex < 0) { ShowError("Select a supplier."); return; }
        if (ProductPicker.SelectedIndex < 0) { ShowError("Select a product."); return; }
        if (!int.TryParse(EntryQuantity.Text, out var quantity) || quantity <= 0) { ShowError("Enter a valid quantity."); return; }
        if (!decimal.TryParse(EntryUnitPrice.Text, out var unitPrice) || unitPrice < 0) { ShowError("Enter a valid unit price."); return; }
        if (!decimal.TryParse(EntryTax.Text, out var tax) || tax < 0 || tax > 100) { ShowError("Tax must be between 0 and 100."); return; }
        if (!decimal.TryParse(EntryPaid.Text, out var paid) || paid < 0) { ShowError("Enter a valid paid amount."); return; }
        var cid   = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        var model = _editing ?? new PurchaseModel { CompanyId = cid };
        var supplier = _supplierItems[SupplierPicker.SelectedIndex];
        var product = _productItems[ProductPicker.SelectedIndex];
        model.SupplierId = supplier.LocalId;
        model.SupplierName = supplier.Name;
        model.PurchaseDate = DatePurchase.Date ?? DateTime.Today;
        model.DueDate = DateDue.Date ?? DateTime.Today;
        model.PaymentMethod = (PaymentMethod)Math.Max(0, PaymentPicker.SelectedIndex);
        model.Status = (PurchaseStatus)Math.Max(0, StatusPicker.SelectedIndex);
        model.Items = [new PurchaseLineItem { ProductId=product.LocalId, ProductName=product.Name, SKU=product.SKU, Quantity=quantity, UnitPrice=unitPrice, TaxPct=tax }];
        model.PaidAmount = paid;
        model.Reference     = EntryRef.Text?.Trim() ?? "";
        model.Notes         = EditorNotes.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(model.PurchaseNumber)) model.PurchaseNumber = await _svc.GetNextPurchaseNumberAsync(cid);

        var (ok, err) = _editing == null ? await _svc.CreateAsync(model) : await _svc.UpdateAsync(model);
        if (ok) await _navigator.GoBackAsync();
        else ShowError(err ?? "Save failed.");
    }

    private void OnProductChanged(object? s, EventArgs e) { if(ProductPicker.SelectedIndex>=0 && ProductPicker.SelectedIndex<_productItems.Count) { var p=_productItems[ProductPicker.SelectedIndex]; EntryUnitPrice.Text=p.PurchasePrice.ToString("0.##"); EntryTax.Text=p.TaxPercent.ToString("0.##"); } }
    private async void OnBackClicked(object s, EventArgs e) => await _navigator.GoBackAsync();
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
