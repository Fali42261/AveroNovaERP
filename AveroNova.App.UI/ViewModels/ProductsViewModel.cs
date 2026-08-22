using System.Collections.ObjectModel;
using System.Diagnostics;
using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Local;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels;

public partial class ProductsViewModel : ObservableObject
{
    public const string StatusFilterAll = "All";
    public const int PageSize = 10;
    private const string RangeDash = "\u2013";

    private readonly IProductService _products;
    private readonly CurrentAccessService _access;
    private readonly IToastService _toasts;
    private int _loadSerial;
    private int _skip;
    private CancellationTokenSource? _searchCts;
    private Guid? _pendingDeleteId;

    public ProductsViewModel(
        IProductService products,
        CurrentAccessService access,
        IToastService toasts)
    {
        _products = products;
        _access = access;
        _toasts = toasts;
        ProductChangeNotifier.Changed += OnProductsChanged;
    }

    public ObservableCollection<ProductModel> Items { get; } = [];

    public IReadOnlyList<string> StatusFilters { get; } =
        [StatusFilterAll, "Active", "Inactive"];

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string selectedStatusFilter = StatusFilterAll;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isDeleting;
    [ObservableProperty] private bool hasLoadError;
    [ObservableProperty] private bool isCompact;
    [ObservableProperty] private bool canView;
    [ObservableProperty] private bool canCreate;
    [ObservableProperty] private bool canUpdate;
    [ObservableProperty] private bool canDelete;
    [ObservableProperty] private int totalCount;
    [ObservableProperty] private string countLabel = "0 products";
    [ObservableProperty] private string showingLabel = string.Empty;
    [ObservableProperty] private string rangeLabel = "1" + RangeDash + "10";

    public bool ShowLoading => IsLoading;
    public bool ShowError => HasLoadError && !IsLoading;
    public bool ShowEmpty => !IsLoading && !HasLoadError && Items.Count == 0;
    public bool ShowList => !IsLoading && !HasLoadError && Items.Count > 0;
    public bool ShowAddButton => CanCreate;
    public bool CanPressAdd => CanCreate && !IsLoading && !IsDeleting;
    public bool ShowEmptyAdd => ShowEmpty && CanCreate && !HasActiveFilter;
    public string? EmptyActionLabel => ShowEmptyAdd ? "+ Add Product" : null;
    public bool ShowDesktopTable => ShowList && !IsCompact;
    public bool ShowMobileCards => ShowList && IsCompact;
    public bool CanRunDelete => CanDelete && !IsDeleting && !IsLoading;
    public bool HasActiveFilter =>
        !string.IsNullOrWhiteSpace(SearchText)
        || !SelectedStatusFilter.Equals(StatusFilterAll, StringComparison.OrdinalIgnoreCase);
    public string EmptyTitle => "No products found.";
    public string EmptySubtitle => HasActiveFilter
        ? string.Empty
        : "Products you add for this company will appear here.";
    public bool ShowPagination => !HasLoadError && TotalCount > 0;
    public bool CanGoPrevious => !IsLoading && !IsDeleting && _skip > 0;
    public bool CanGoNext => !IsLoading && !IsDeleting && _skip + PageSize < TotalCount;

    public event EventHandler? AddRequested;
    public event EventHandler<ProductModel>? ViewRequested;
    public event EventHandler<ProductModel>? EditRequested;

    public void ResetPaging() => _skip = 0;

    public async Task LoadAsync(bool showLoading = true)
    {
        var serial = ++_loadSerial;
        if (showLoading)
            IsLoading = true;
        HasLoadError = false;
        NotifyUiState();

        try
        {
            await RefreshPermissionsAsync();
            if (!CanView)
            {
                Items.Clear();
                TotalCount = 0;
                UpdatePagingLabels();
                return;
            }

            var result = await QueryPageAsync();

            if (result.Items.Count == 0 && result.TotalCount > 0 && _skip > 0)
            {
                _skip = ((result.TotalCount - 1) / PageSize) * PageSize;
                result = await QueryPageAsync();
            }

            if (serial != _loadSerial)
                return;

            Items.Clear();
            foreach (var item in result.Items)
                Items.Add(item);

            TotalCount = result.TotalCount;
            UpdatePagingLabels();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AveroNova] Products load failed: {ex.Message}");
            if (serial != _loadSerial)
                return;
            HasLoadError = true;
            Items.Clear();
            TotalCount = 0;
            UpdatePagingLabels();
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
    private Task RetryAsync() => LoadAsync();

    [RelayCommand(CanExecute = nameof(CanPressAdd))]
    private void Add()
    {
        if (!CanPressAdd)
            return;
        AddRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void View(ProductModel? product)
    {
        if (!CanView || product == null)
            return;
        ViewRequested?.Invoke(this, product);
    }

    [RelayCommand]
    private void Edit(ProductModel? product)
    {
        if (!CanUpdate || product == null || IsLoading || IsDeleting)
            return;
        EditRequested?.Invoke(this, product);
    }

    [RelayCommand(CanExecute = nameof(CanRunDelete))]
    private async Task DeleteAsync(ProductModel? product)
    {
        if (!CanRunDelete || product == null)
            return;

        var confirmed = await DialogHelper.ConfirmDeleteAsync(
            "Product",
            "Are you sure you want to delete this product?");
        if (!confirmed)
            return;

        await ExecuteDeleteAsync(product.LocalId);
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private Task PreviousAsync()
    {
        if (!CanGoPrevious)
            return Task.CompletedTask;
        _skip = Math.Max(0, _skip - PageSize);
        return LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private Task NextAsync()
    {
        if (!CanGoNext)
            return Task.CompletedTask;
        _skip += PageSize;
        return LoadAsync();
    }

    public Task ConfirmDeleteAsync(Guid id) => ExecuteDeleteAsync(id);

    private async Task ExecuteDeleteAsync(Guid id)
    {
        if (!CanRunDelete || id == Guid.Empty || _pendingDeleteId != null)
            return;

        _pendingDeleteId = id;
        IsDeleting = true;
        NotifyUiState();
        try
        {
            var (ok, error) = await _products.DeleteAsync(id);
            if (ok)
            {
                _toasts.ShowSuccess("Product deleted successfully.", "The product has been removed.");
                await LoadAsync(showLoading: false);
                return;
            }

            _toasts.ShowError(
                "Unable to delete product.",
                string.IsNullOrWhiteSpace(error) ? "Please try again." : error);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AveroNova] Product delete failed: {ex.Message}");
            _toasts.ShowError("Unable to delete product.", "Please try again.");
        }
        finally
        {
            _pendingDeleteId = null;
            IsDeleting = false;
            NotifyUiState();
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _skip = 0;
        QueueReload();
    }

    partial void OnSelectedStatusFilterChanged(string value)
    {
        _skip = 0;
        QueueReload();
    }

    partial void OnIsCompactChanged(bool value) => NotifyUiState();

    private void QueueReload()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        _ = DebouncedReloadAsync(token);
    }

    private async Task DebouncedReloadAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token);
            await LoadAsync();
        }
        catch (TaskCanceledException)
        {
        }
    }

    private Task<ProductListResult> QueryPageAsync()
        => _products.QueryAsync(new ProductListQuery
        {
            SearchText = SearchText,
            Status = ParseStatusFilter(SelectedStatusFilter),
            Skip = _skip,
            Take = PageSize
        });

    private async Task RefreshPermissionsAsync()
    {
        var snapshot = await _access.GetSnapshotAsync();
        CanView = PermissionNames.Grants(snapshot.Permissions, PermissionNames.ProductsView);
        var manage = PermissionNames.Grants(snapshot.Permissions, PermissionNames.ProductsManage);
        CanCreate = manage;
        CanUpdate = manage;
        CanDelete = manage;
    }

    private void OnProductsChanged(object? sender, EventArgs e)
        => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync(showLoading: false));

    private void UpdatePagingLabels()
    {
        CountLabel = $"{TotalCount} product{(TotalCount == 1 ? string.Empty : "s")}";
        if (TotalCount <= 0 || Items.Count == 0)
        {
            ShowingLabel = string.Empty;
            RangeLabel = "0" + RangeDash + "0";
            return;
        }

        var start = _skip + 1;
        var end = _skip + Items.Count;
        RangeLabel = $"{start}{RangeDash}{end}";
        var noun = TotalCount == 1 ? "product" : "products";
        ShowingLabel = $"Showing {start}{RangeDash}{end} of {TotalCount} {noun}";
    }

    private void NotifyUiState()
    {
        OnPropertyChanged(nameof(ShowLoading));
        OnPropertyChanged(nameof(ShowError));
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowList));
        OnPropertyChanged(nameof(ShowAddButton));
        OnPropertyChanged(nameof(CanPressAdd));
        OnPropertyChanged(nameof(ShowEmptyAdd));
        OnPropertyChanged(nameof(EmptyActionLabel));
        OnPropertyChanged(nameof(ShowDesktopTable));
        OnPropertyChanged(nameof(ShowMobileCards));
        OnPropertyChanged(nameof(CanRunDelete));
        OnPropertyChanged(nameof(HasActiveFilter));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptySubtitle));
        OnPropertyChanged(nameof(ShowPagination));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        AddCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    private static ProductStatus? ParseStatusFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals(StatusFilterAll, StringComparison.OrdinalIgnoreCase))
            return null;
        return Enum.TryParse<ProductStatus>(value, true, out var status) ? status : null;
    }
}
