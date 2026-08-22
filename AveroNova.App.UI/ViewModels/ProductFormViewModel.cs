using System.Diagnostics;
using System.Globalization;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Local;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels;

public partial class ProductFormViewModel : ObservableObject
{
    private readonly IProductService _products;
    private readonly CurrentAccessService _access;
    private readonly IToastService _toasts;
    private Guid? _editingId;
    private int _loadSerial;
    private int _loadedOpeningStock;
    private int _loadedStock;

    public ProductFormViewModel(
        IProductService products,
        CurrentAccessService access,
        IToastService toasts)
    {
        _products = products;
        _access = access;
        _toasts = toasts;
    }

    public IReadOnlyList<string> StatusOptions { get; } = ["Active", "Inactive"];

    public IReadOnlyList<string> UnitOptions { get; } =
        ["pcs", "box", "kg", "g", "ltr", "m", "pack", "set"];

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private bool hasLoadError;
    [ObservableProperty] private bool isEditMode;
    [ObservableProperty] private bool canSaveRecord;
    [ObservableProperty] private string pageTitle = "Add Product";
    [ObservableProperty] private Guid savedId;

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string sku = string.Empty;
    [ObservableProperty] private string barcode = string.Empty;
    [ObservableProperty] private string category = string.Empty;
    [ObservableProperty] private string brand = string.Empty;
    [ObservableProperty] private string unit = "pcs";
    [ObservableProperty] private string purchasePriceText = string.Empty;
    [ObservableProperty] private string salePriceText = string.Empty;
    [ObservableProperty] private string taxText = string.Empty;
    [ObservableProperty] private string discountText = string.Empty;
    [ObservableProperty] private string openingStockText = string.Empty;
    [ObservableProperty] private string minimumStockText = string.Empty;
    [ObservableProperty] private string selectedStatus = "Active";

    [ObservableProperty] private string nameError = string.Empty;
    [ObservableProperty] private bool hasNameError;
    [ObservableProperty] private string skuError = string.Empty;
    [ObservableProperty] private bool hasSkuError;
    [ObservableProperty] private string barcodeError = string.Empty;
    [ObservableProperty] private bool hasBarcodeError;
    [ObservableProperty] private string salePriceError = string.Empty;
    [ObservableProperty] private bool hasSalePriceError;
    [ObservableProperty] private string purchasePriceError = string.Empty;
    [ObservableProperty] private bool hasPurchasePriceError;
    [ObservableProperty] private string taxError = string.Empty;
    [ObservableProperty] private bool hasTaxError;
    [ObservableProperty] private string discountError = string.Empty;
    [ObservableProperty] private bool hasDiscountError;
    [ObservableProperty] private string openingStockError = string.Empty;
    [ObservableProperty] private bool hasOpeningStockError;
    [ObservableProperty] private string minimumStockError = string.Empty;
    [ObservableProperty] private bool hasMinimumStockError;

    public Guid? EditingId => _editingId;
    public bool ShowLoading => IsLoading;
    public bool ShowError => HasLoadError && !IsLoading;
    public bool ShowForm => !IsLoading && !HasLoadError;
    public bool IsOpeningStockReadOnly => IsEditMode;
    public string SaveButtonText => IsSaving ? "Saving..." : IsEditMode ? "Save Changes" : "Save";
    public bool CanPressSave => CanSaveRecord && ShowForm && !IsSaving;

    public event EventHandler? Saved;
    public event EventHandler? Cancelled;

    partial void OnIsEditModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsOpeningStockReadOnly));
        OnPropertyChanged(nameof(SaveButtonText));
    }

    public async Task InitializeAsync(Guid? productId)
    {
        var serial = ++_loadSerial;
        _editingId = productId;
        SavedId = Guid.Empty;
        IsEditMode = productId.HasValue && productId.Value != Guid.Empty;
        PageTitle = IsEditMode ? "Edit Product" : "Add Product";
        IsLoading = IsEditMode;
        HasLoadError = false;
        ClearValidation();
        NotifyUiState();

        try
        {
            await RefreshPermissionsAsync();
            if (!CanSaveRecord)
            {
                HasLoadError = true;
                return;
            }

            if (!IsEditMode)
            {
                ResetFields();
                return;
            }

            var product = await _products.GetByIdAsync(productId!.Value);
            if (serial != _loadSerial)
                return;
            if (product == null)
            {
                HasLoadError = true;
                return;
            }

            Apply(product);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AveroNova] Product form load failed: {ex.Message}");
            if (serial != _loadSerial)
                return;
            HasLoadError = true;
        }
        finally
        {
            if (serial == _loadSerial)
            {
                IsLoading = false;
                NotifyUiState();
            }
        }
    }

    [RelayCommand]
    private Task RetryAsync() => InitializeAsync(_editingId);

    [RelayCommand(CanExecute = nameof(CanPressSave))]
    private async Task SaveAsync()
    {
        if (!CanPressSave)
            return;
        if (!TryBuildModel(out var model))
            return;

        IsSaving = true;
        NotifyUiState();
        try
        {
            if (IsEditMode)
            {
                var (updated, updateError) = await _products.UpdateAsync(model);
                if (updated)
                {
                    var reloaded = await _products.GetByIdAsync(model.LocalId);
                    if (reloaded == null || reloaded.CompanyId == Guid.Empty)
                    {
                        _toasts.ShowError("Unable to update product.", "Please try again.");
                        return;
                    }

                    SavedId = reloaded.LocalId;
                    _toasts.ShowSuccess("Product updated successfully.", "The product details have been saved.");
                    Saved?.Invoke(this, EventArgs.Empty);
                    return;
                }

                ApplyServiceFieldErrors(updateError);
                _toasts.ShowError(
                    "Unable to update product.",
                    string.IsNullOrWhiteSpace(updateError) ? "Please try again." : updateError);
                return;
            }

            var (ok, error) = await _products.CreateAsync(model);
            if (ok)
            {
                var saved = await _products.GetByIdAsync(model.LocalId);
                if (saved == null || saved.CompanyId == Guid.Empty)
                {
                    ok = false;
                    error = "Unable to save product.";
                }
                else
                {
                    SavedId = saved.LocalId;
                    _toasts.ShowSuccess("Product created successfully.", "The product has been saved.");
                    Saved?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }

            ApplyServiceFieldErrors(error);
            _toasts.ShowError(
                "Unable to save product.",
                string.IsNullOrWhiteSpace(error) ? "Please try again." : error);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AveroNova] Product save failed: {ex.Message}");
            _toasts.ShowError(
                IsEditMode ? "Unable to update product." : "Unable to save product.",
                "Please try again.");
        }
        finally
        {
            IsSaving = false;
            NotifyUiState();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsSaving)
            return;
        ResetFields();
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private async Task RefreshPermissionsAsync()
    {
        var snapshot = await _access.GetSnapshotAsync();
        CanSaveRecord = PermissionNames.Grants(snapshot.Permissions, PermissionNames.ProductsManage);
    }

    private void Apply(ProductModel product)
    {
        Name = product.Name ?? string.Empty;
        Sku = product.SKU ?? string.Empty;
        Barcode = product.Barcode ?? string.Empty;
        Category = product.Category ?? string.Empty;
        Brand = product.Brand ?? string.Empty;
        var loadedUnit = string.IsNullOrWhiteSpace(product.Unit) ? "pcs" : product.Unit.Trim();
        Unit = UnitOptions.FirstOrDefault(option => option.Equals(loadedUnit, StringComparison.OrdinalIgnoreCase))
               ?? loadedUnit;
        PurchasePriceText = FormatLoadedDecimal(product.PurchasePrice);
        SalePriceText = FormatLoadedDecimal(product.SellingPrice);
        TaxText = FormatLoadedDecimal(product.TaxPercent);
        DiscountText = FormatLoadedDecimal(product.DiscountPercent);
        OpeningStockText = product.OpeningStock.ToString(CultureInfo.CurrentCulture);
        MinimumStockText = product.MinimumStock.ToString(CultureInfo.CurrentCulture);
        SelectedStatus = product.Status == ProductStatus.Inactive ? "Inactive" : "Active";
        _loadedOpeningStock = product.OpeningStock;
        _loadedStock = product.Stock;
        ClearValidation();
    }

    private void ResetFields()
    {
        Name = Sku = Barcode = Category = Brand = string.Empty;
        Unit = "pcs";
        PurchasePriceText = SalePriceText = TaxText = DiscountText = string.Empty;
        OpeningStockText = MinimumStockText = string.Empty;
        SelectedStatus = "Active";
        _loadedOpeningStock = 0;
        _loadedStock = 0;
        ClearValidation();
    }

    private bool TryBuildModel(out ProductModel model)
    {
        model = new ProductModel();
        ClearValidation();
        var isValid = true;

        if (string.IsNullOrWhiteSpace(Name))
        {
            NameError = "Product name is required.";
            HasNameError = true;
            isValid = false;
        }
        else if (Name.Trim().Length > 200)
        {
            NameError = "Product name must be 200 characters or fewer.";
            HasNameError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(Sku))
        {
            SkuError = "SKU / Product Code is required.";
            HasSkuError = true;
            isValid = false;
        }
        else if (Sku.Trim().Length > 50)
        {
            SkuError = "SKU / Product Code must be 50 characters or fewer.";
            HasSkuError = true;
            isValid = false;
        }

        if (!string.IsNullOrWhiteSpace(Barcode) && Barcode.Trim().Length > 50)
        {
            BarcodeError = "Barcode must be 50 characters or fewer.";
            HasBarcodeError = true;
            isValid = false;
        }

        if (!TryParseRequiredDecimal(SalePriceText, "Sale price is required.", out var salePrice, out var saleError)
            || salePrice < 0)
        {
            SalePriceError = saleError ?? "Sale price cannot be negative.";
            HasSalePriceError = true;
            isValid = false;
            salePrice = 0;
        }

        if (!TryParseOptionalDecimal(PurchasePriceText, out var purchasePrice) || purchasePrice < 0)
        {
            PurchasePriceError = "Enter a valid purchase price.";
            HasPurchasePriceError = true;
            isValid = false;
            purchasePrice = 0;
        }

        if (!TryParseOptionalDecimal(TaxText, out var tax) || tax < 0 || tax > 100)
        {
            TaxError = "Tax / GST must be between 0 and 100.";
            HasTaxError = true;
            isValid = false;
            tax = 0;
        }

        if (!TryParseOptionalDecimal(DiscountText, out var discount) || discount < 0 || discount > 100)
        {
            DiscountError = "Discount must be between 0 and 100.";
            HasDiscountError = true;
            isValid = false;
            discount = 0;
        }

        int opening;
        if (IsEditMode)
        {
            opening = _loadedOpeningStock;
        }
        else if (!TryParseOptionalInt(OpeningStockText, out opening) || opening < 0)
        {
            OpeningStockError = "Opening stock cannot be negative.";
            HasOpeningStockError = true;
            isValid = false;
            opening = 0;
        }

        if (!TryParseOptionalInt(MinimumStockText, out var minimum) || minimum < 0)
        {
            MinimumStockError = "Minimum stock cannot be negative.";
            HasMinimumStockError = true;
            isValid = false;
            minimum = 0;
        }

        if (!isValid)
            return false;

        model = new ProductModel
        {
            LocalId = _editingId ?? Guid.Empty,
            Name = Name.Trim(),
            SKU = Sku.Trim(),
            Barcode = Barcode.Trim(),
            Category = Category.Trim(),
            Brand = Brand.Trim(),
            Unit = string.IsNullOrWhiteSpace(Unit) ? "pcs" : Unit.Trim(),
            PurchasePrice = purchasePrice,
            SellingPrice = salePrice,
            TaxPercent = tax,
            DiscountPercent = discount,
            OpeningStock = opening,
            Stock = IsEditMode ? _loadedStock : 0,
            MinimumStock = minimum,
            Status = Enum.TryParse<ProductStatus>(SelectedStatus, true, out var status)
                ? status
                : ProductStatus.Active
        };
        return true;
    }

    private void ClearValidation()
    {
        NameError = SkuError = BarcodeError = SalePriceError = PurchasePriceError = TaxError = DiscountError
            = OpeningStockError = MinimumStockError = string.Empty;
        HasNameError = HasSkuError = HasBarcodeError = HasSalePriceError = HasPurchasePriceError = HasTaxError
            = HasDiscountError = HasOpeningStockError = HasMinimumStockError = false;
    }

    private void ApplyServiceFieldErrors(string? error)
    {
        if (string.Equals(error, LocalProductService.DuplicateSkuMessage, StringComparison.Ordinal))
        {
            SkuError = LocalProductService.DuplicateSkuMessage;
            HasSkuError = true;
        }
        else if (string.Equals(error, LocalProductService.DuplicateBarcodeMessage, StringComparison.Ordinal))
        {
            BarcodeError = LocalProductService.DuplicateBarcodeMessage;
            HasBarcodeError = true;
        }
    }

    private void NotifyUiState()
    {
        OnPropertyChanged(nameof(ShowLoading));
        OnPropertyChanged(nameof(ShowError));
        OnPropertyChanged(nameof(ShowForm));
        OnPropertyChanged(nameof(IsOpeningStockReadOnly));
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(CanPressSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    private static string FormatLoadedDecimal(decimal value)
        => value.ToString("0.##", CultureInfo.CurrentCulture);

    private static bool TryParseRequiredDecimal(string? text, string requiredMessage, out decimal value, out string? error)
    {
        value = 0;
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = requiredMessage;
            return false;
        }

        if (!decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            && !decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            error = "Enter a valid sale price.";
            return false;
        }

        return true;
    }

    private static bool TryParseOptionalDecimal(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        return decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseOptionalInt(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
            || int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
