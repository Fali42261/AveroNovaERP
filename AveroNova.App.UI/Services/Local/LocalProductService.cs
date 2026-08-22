using AveroNova.Application.Interfaces.Repositories;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;
using AveroNova.Domain.Entities;

namespace AveroNova.App.UI.Services.Local;

public sealed class LocalProductService : IProductService
{
    private readonly IProductRepository _products;
    private readonly CurrentAccessService _access;

    public LocalProductService(
        IProductRepository products,
        CurrentAccessService access)
    {
        _products = products;
        _access = access;
    }

    public event EventHandler? ProductsChanged;

    public async Task<List<ProductModel>> GetAllAsync(Guid companyId)
    {
        var result = await QueryInternalAsync(new ProductListQuery());
        return result.Items.ToList();
    }

    public async Task<ProductModel?> GetByIdAsync(Guid id)
    {
        var companyId = CurrentCompanyId();
        if (companyId == Guid.Empty || id == Guid.Empty)
            return null;
        if (!await CanAsync(PermissionNames.ProductsView))
            return null;

        var entity = await _products.GetByIdAsync(companyId, id);
        return entity == null ? null : Map(entity);
    }

    public const string DuplicateSkuMessage = "SKU already exists.";
    public const string DuplicateBarcodeMessage = "Barcode already exists.";

    public async Task<(bool Ok, string? Error)> CreateAsync(ProductModel product)
    {
        var companyId = CurrentCompanyId();
        if (companyId == Guid.Empty)
            return (false, "Unable to save product.");
        if (!await CanAsync(PermissionNames.ProductsManage))
            return (false, "You do not have permission to create products.");

        // UI-supplied CompanyId is ignored; the product always belongs to the session company.
        product.CompanyId = Guid.Empty;

        var error = Validate(product);
        if (error != null)
            return (false, error);

        var sku = Clamp(product.SKU, 50);
        var barcode = Clamp(product.Barcode, 50);
        if (await _products.ExistsBySkuAsync(companyId, sku, null))
            return (false, DuplicateSkuMessage);
        if (barcode.Length > 0 && await _products.ExistsByBarcodeAsync(companyId, barcode, null))
            return (false, DuplicateBarcodeMessage);

        var now = DateTime.UtcNow;
        var opening = Math.Max(0, product.OpeningStock);
        var entity = new Product
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = Clamp(product.Name, 200),
            SKU = sku,
            Barcode = barcode,
            Category = Clamp(product.Category, 100),
            Brand = Clamp(product.Brand, 100),
            Unit = string.IsNullOrWhiteSpace(product.Unit) ? "pcs" : Clamp(product.Unit, 20),
            PurchasePrice = product.PurchasePrice,
            SellingPrice = product.SellingPrice,
            TaxPercent = product.TaxPercent,
            DiscountPercent = product.DiscountPercent,
            OpeningStock = opening,
            Stock = opening,
            MinimumStock = Math.Max(0, product.MinimumStock),
            Description = Clamp(product.Description, 1000),
            Status = (int)product.Status,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        try
        {
            await _products.AddAsync(entity);
            var saved = await _products.GetByIdAsync(companyId, entity.Id);
            if (saved == null || !MatchesCreated(saved, entity))
                return (false, "Unable to save product.");

            product.LocalId = saved.Id;
            product.CompanyId = saved.CompanyId;
            product.Name = saved.Name;
            product.SKU = saved.SKU;
            product.Barcode = saved.Barcode;
            product.Category = saved.Category;
            product.Brand = saved.Brand;
            product.Unit = saved.Unit;
            product.PurchasePrice = saved.PurchasePrice;
            product.SellingPrice = saved.SellingPrice;
            product.TaxPercent = saved.TaxPercent;
            product.DiscountPercent = saved.DiscountPercent;
            product.Stock = saved.Stock;
            product.OpeningStock = saved.OpeningStock;
            product.MinimumStock = saved.MinimumStock;
            product.Status = Enum.IsDefined(typeof(ProductStatus), saved.Status)
                ? (ProductStatus)saved.Status
                : ProductStatus.Active;
            product.CreatedAt = saved.CreatedAt;
            product.UpdatedAt = saved.UpdatedAt ?? now;
            product.SyncStatus = SyncStatus.PendingSync;
            RaiseChanged();
            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Product create failed: {ex.Message}");
            return (false, "Unable to save product.");
        }
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(ProductModel product)
    {
        var companyId = CurrentCompanyId();
        if (companyId == Guid.Empty || product.LocalId == Guid.Empty)
            return (false, "Unable to update product.");
        if (!await CanAsync(PermissionNames.ProductsManage))
            return (false, "You do not have permission to update products.");

        var error = Validate(product);
        if (error != null)
            return (false, error);

        var existing = await _products.GetByIdAsync(companyId, product.LocalId);
        if (existing == null)
            return (false, "Product not found.");

        var sku = Clamp(product.SKU, 50);
        var barcode = Clamp(product.Barcode, 50);
        if (await _products.ExistsBySkuAsync(companyId, sku, existing.Id))
            return (false, DuplicateSkuMessage);
        if (barcode.Length > 0 && await _products.ExistsByBarcodeAsync(companyId, barcode, existing.Id))
            return (false, DuplicateBarcodeMessage);

        var now = DateTime.UtcNow;
        existing.Name = Clamp(product.Name, 200);
        existing.SKU = sku;
        existing.Barcode = barcode;
        existing.Category = Clamp(product.Category, 100);
        existing.Brand = Clamp(product.Brand, 100);
        existing.Unit = string.IsNullOrWhiteSpace(product.Unit) ? "pcs" : Clamp(product.Unit, 20);
        existing.PurchasePrice = product.PurchasePrice;
        existing.SellingPrice = product.SellingPrice;
        existing.TaxPercent = product.TaxPercent;
        existing.DiscountPercent = product.DiscountPercent;
        existing.MinimumStock = Math.Max(0, product.MinimumStock);
        existing.Description = Clamp(product.Description, 1000);
        existing.Status = (int)product.Status;
        existing.UpdatedAt = now;
        existing.CompanyId = companyId;

        try
        {
            await _products.UpdateAsync(existing);
            var saved = await _products.GetByIdAsync(companyId, existing.Id);
            if (saved == null || saved.CompanyId != companyId || !MatchesUpdated(saved, existing))
                return (false, "Unable to update product.");

            product.LocalId = saved.Id;
            product.CompanyId = saved.CompanyId;
            product.Name = saved.Name;
            product.SKU = saved.SKU;
            product.Barcode = saved.Barcode;
            product.Category = saved.Category;
            product.Brand = saved.Brand;
            product.Unit = saved.Unit;
            product.PurchasePrice = saved.PurchasePrice;
            product.SellingPrice = saved.SellingPrice;
            product.TaxPercent = saved.TaxPercent;
            product.DiscountPercent = saved.DiscountPercent;
            product.Stock = saved.Stock;
            product.OpeningStock = saved.OpeningStock;
            product.MinimumStock = saved.MinimumStock;
            product.Status = Enum.IsDefined(typeof(ProductStatus), saved.Status)
                ? (ProductStatus)saved.Status
                : ProductStatus.Active;
            product.CreatedAt = saved.CreatedAt;
            product.UpdatedAt = saved.UpdatedAt ?? now;
            product.SyncStatus = SyncStatus.PendingSync;
            RaiseChanged();
            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Product update failed: {ex.Message}");
            return (false, "Unable to update product.");
        }
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        var companyId = CurrentCompanyId();
        if (companyId == Guid.Empty || id == Guid.Empty)
            return (false, "Unable to delete product.");

        if (!await CanAsync(PermissionNames.ProductsManage))
            return (false, "You do not have permission to delete products.");

        var existing = await _products.GetByIdAsync(companyId, id);
        if (existing == null)
            return (false, "Product not found.");

        try
        {
            var deleted = await _products.SoftDeleteAsync(companyId, id);
            if (!deleted)
                return (false, "Product not found.");

            RaiseChanged();
            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Product delete failed: {ex.Message}");
            return (false, "Unable to delete product.");
        }
    }

    public Task<List<ProductModel>> SearchAsync(Guid companyId, string query)
        => QueryToListAsync(new ProductListQuery { SearchText = query });

    public async Task<List<ProductModel>> GetLowStockAsync(Guid companyId)
    {
        var currentCompanyId = CurrentCompanyId();
        if (currentCompanyId == Guid.Empty)
            return [];

        var items = await _products.GetLowStockAsync(currentCompanyId);
        return items.Select(Map).ToList();
    }

    public async Task<ProductListResult> QueryAsync(ProductListQuery query)
    {
        if (!await CanAsync(PermissionNames.ProductsView))
            return new ProductListResult();

        return await QueryInternalAsync(query);
    }

    private async Task<List<ProductModel>> QueryToListAsync(ProductListQuery query)
    {
        var result = await QueryAsync(query);
        return result.Items.ToList();
    }

    private async Task<ProductListResult> QueryInternalAsync(ProductListQuery query)
    {
        var companyId = CurrentCompanyId();
        if (companyId == Guid.Empty)
            return new ProductListResult();

        var status = query.Status.HasValue ? (int?)query.Status.Value : null;
        var (items, total) = await _products.QueryAsync(
            companyId,
            query.SearchText,
            status,
            query.Skip,
            query.Take);

        return new ProductListResult
        {
            Items = items.Select(Map).ToList(),
            TotalCount = total
        };
    }

    private void RaiseChanged()
    {
        ProductsChanged?.Invoke(this, EventArgs.Empty);
        ProductChangeNotifier.Notify();
    }

    private static bool MatchesCreated(Product saved, Product expected)
        => saved.Id == expected.Id
           && saved.CompanyId == expected.CompanyId
           && string.Equals(saved.Name, expected.Name, StringComparison.Ordinal)
           && string.Equals(saved.SKU, expected.SKU, StringComparison.Ordinal)
           && saved.SellingPrice == expected.SellingPrice
           && !saved.IsDeleted;

    private static bool MatchesUpdated(Product saved, Product expected)
        => saved.Id == expected.Id
           && saved.CompanyId == expected.CompanyId
           && string.Equals(saved.Name, expected.Name, StringComparison.Ordinal)
           && string.Equals(saved.SKU, expected.SKU, StringComparison.Ordinal)
           && string.Equals(saved.Barcode, expected.Barcode, StringComparison.Ordinal)
           && string.Equals(saved.Category, expected.Category, StringComparison.Ordinal)
           && string.Equals(saved.Brand, expected.Brand, StringComparison.Ordinal)
           && string.Equals(saved.Unit, expected.Unit, StringComparison.Ordinal)
           && saved.PurchasePrice == expected.PurchasePrice
           && saved.SellingPrice == expected.SellingPrice
           && saved.TaxPercent == expected.TaxPercent
           && saved.DiscountPercent == expected.DiscountPercent
           && saved.MinimumStock == expected.MinimumStock
           && saved.OpeningStock == expected.OpeningStock
           && saved.Stock == expected.Stock
           && saved.Status == expected.Status
           && !saved.IsDeleted;

    private async Task<bool> CanAsync(string permission)
    {
        var snapshot = await _access.GetSnapshotAsync();
        return PermissionNames.Grants(snapshot.Permissions, permission);
    }

    private static Guid CurrentCompanyId()
        => LocalSessionStore.CompanyId ?? Guid.Empty;

    private static string? Validate(ProductModel product)
    {
        if (string.IsNullOrWhiteSpace(product.Name))
            return "Product name is required.";
        if (product.Name.Trim().Length > 200)
            return "Product name must be 200 characters or fewer.";

        if (string.IsNullOrWhiteSpace(product.SKU))
            return "SKU / Product Code is required.";
        if (product.SKU.Trim().Length > 50)
            return "SKU / Product Code must be 50 characters or fewer.";

        if (product.SellingPrice < 0)
            return "Sale price cannot be negative.";

        if (product.PurchasePrice < 0)
            return "Purchase price cannot be negative.";

        if (product.TaxPercent < 0 || product.TaxPercent > 100)
            return "Tax / GST must be between 0 and 100.";

        if (product.DiscountPercent < 0 || product.DiscountPercent > 100)
            return "Discount must be between 0 and 100.";

        if (product.OpeningStock < 0 || product.Stock < 0 || product.MinimumStock < 0)
            return "Stock cannot be negative.";

        return null;
    }

    private static string Clamp(string? value, int maxLength)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static ProductModel Map(Product product) => new()
    {
        LocalId = product.Id,
        CompanyId = product.CompanyId,
        Name = product.Name,
        SKU = product.SKU,
        Barcode = product.Barcode,
        Category = product.Category,
        Brand = product.Brand,
        Unit = product.Unit,
        PurchasePrice = product.PurchasePrice,
        SellingPrice = product.SellingPrice,
        TaxPercent = product.TaxPercent,
        DiscountPercent = product.DiscountPercent,
        Stock = product.Stock,
        OpeningStock = product.OpeningStock,
        MinimumStock = product.MinimumStock,
        Description = product.Description,
        Status = Enum.IsDefined(typeof(ProductStatus), product.Status)
            ? (ProductStatus)product.Status
            : ProductStatus.Active,
        CreatedAt = product.CreatedAt,
        UpdatedAt = product.UpdatedAt ?? product.CreatedAt,
        IsDeleted = product.IsDeleted,
        SyncStatus = SyncStatus.Local
    };
}
