using System.Diagnostics;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels;

public partial class ProductViewViewModel : ObservableObject
{
    private readonly IProductService _products;
    private readonly CurrentAccessService _access;
    private ProductModel? _loaded;
    private Guid _productId;
    private int _loadSerial;

    public ProductViewViewModel(
        IProductService products,
        CurrentAccessService access)
    {
        _products = products;
        _access = access;
    }

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool hasLoadError;
    [ObservableProperty] private bool isNotFound;
    [ObservableProperty] private bool canView;
    [ObservableProperty] private bool canUpdate;
    [ObservableProperty] private int cardColumns = 3;

    [ObservableProperty] private string displayName = "—";
    [ObservableProperty] private string nameTooltip = string.Empty;
    [ObservableProperty] private string displaySku = "—";
    [ObservableProperty] private string displayBarcode = "—";
    [ObservableProperty] private string displayCategory = "—";
    [ObservableProperty] private string displayBrand = "—";
    [ObservableProperty] private string displayUnit = "—";
    [ObservableProperty] private string displayPurchasePrice = "—";
    [ObservableProperty] private string displaySalePrice = "—";
    [ObservableProperty] private string displayTax = "—";
    [ObservableProperty] private string displayDiscount = "—";
    [ObservableProperty] private string displayStock = "—";
    [ObservableProperty] private string displayOpeningStock = "—";
    [ObservableProperty] private string displayMinimumStock = "—";
    [ObservableProperty] private string displayReorderLevel = "—";
    [ObservableProperty] private string displayStatus = "—";

    public bool ShowLoading => IsLoading;
    public bool ShowError => HasLoadError && !IsLoading && !IsNotFound;
    public bool ShowNotFound => IsNotFound && !IsLoading;
    public bool ShowContent => !IsLoading && !HasLoadError && !IsNotFound;
    public bool ShowEditButton => ShowContent && CanUpdate;

    public event EventHandler? BackRequested;
    public event EventHandler<Guid>? EditRequested;

    public Guid CurrentId => _productId;

    public async Task LoadAsync(Guid productId, bool showLoading = true)
    {
        var serial = ++_loadSerial;
        _productId = productId;
        if (showLoading)
            IsLoading = true;
        HasLoadError = false;
        IsNotFound = false;
        ClearDisplay();
        NotifyUiState();

        try
        {
            await RefreshPermissionsAsync();
            if (!CanView)
            {
                if (serial != _loadSerial)
                    return;
                IsNotFound = true;
                _loaded = null;
                return;
            }

            var product = await _products.GetByIdAsync(productId);
            if (serial != _loadSerial)
                return;
            if (product == null)
            {
                IsNotFound = true;
                _loaded = null;
                return;
            }

            Apply(product);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AveroNova] Product details load failed: {ex.Message}");
            if (serial != _loadSerial)
                return;
            HasLoadError = true;
            _loaded = null;
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
    private Task RetryAsync() => LoadAsync(_productId);

    [RelayCommand]
    private void Back() => BackRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Edit()
    {
        if (!CanUpdate || _loaded == null)
            return;
        EditRequested?.Invoke(this, _loaded.LocalId);
    }

    private async Task RefreshPermissionsAsync()
    {
        var snapshot = await _access.GetSnapshotAsync();
        CanView = PermissionNames.Grants(snapshot.Permissions, PermissionNames.ProductsView);
        CanUpdate = PermissionNames.Grants(snapshot.Permissions, PermissionNames.ProductsManage);
    }

    private void Apply(ProductModel product)
    {
        _loaded = product;
        DisplayName = Dash(product.Name);
        NameTooltip = product.Name?.Trim() ?? string.Empty;
        DisplaySku = Dash(product.SKU);
        DisplayBarcode = Dash(product.Barcode);
        DisplayCategory = Dash(product.Category);
        DisplayBrand = Dash(product.Brand);
        DisplayUnit = Dash(product.Unit);
        DisplayPurchasePrice = Money(product.PurchasePrice);
        DisplaySalePrice = Money(product.SellingPrice);
        DisplayTax = $"{product.TaxPercent:0.##}%";
        DisplayDiscount = $"{product.DiscountPercent:0.##}%";
        DisplayStock = product.Stock.ToString();
        DisplayOpeningStock = product.OpeningStock.ToString();
        DisplayMinimumStock = product.MinimumStock.ToString();
        DisplayReorderLevel = product.MinimumStock.ToString();
        DisplayStatus = Dash(product.StatusLabel);
    }

    private void ClearDisplay()
    {
        _loaded = null;
        DisplayName = "—";
        NameTooltip = string.Empty;
        DisplaySku = "—";
        DisplayBarcode = "—";
        DisplayCategory = "—";
        DisplayBrand = "—";
        DisplayUnit = "—";
        DisplayPurchasePrice = "—";
        DisplaySalePrice = "—";
        DisplayTax = "—";
        DisplayDiscount = "—";
        DisplayStock = "—";
        DisplayOpeningStock = "—";
        DisplayMinimumStock = "—";
        DisplayReorderLevel = "—";
        DisplayStatus = "—";
    }

    private void NotifyUiState()
    {
        OnPropertyChanged(nameof(ShowLoading));
        OnPropertyChanged(nameof(ShowError));
        OnPropertyChanged(nameof(ShowNotFound));
        OnPropertyChanged(nameof(ShowContent));
        OnPropertyChanged(nameof(ShowEditButton));
    }

    private static string Dash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string Money(decimal value)
        => $"₹{value:N2}";
}
