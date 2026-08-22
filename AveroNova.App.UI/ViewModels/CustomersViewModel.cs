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

public partial class CustomersViewModel : ObservableObject
{
    public const string StatusFilterAll = "All";
    public const int PageSize = 10;
    private const string RangeDash = "\u2013";

    private readonly ICustomerService _customers;
    private readonly CurrentAccessService _access;
    private readonly IToastService _toasts;
    private int _loadSerial;
    private int _skip;
    private CancellationTokenSource? _searchCts;
    private Guid? _pendingDeleteId;

    public CustomersViewModel(
        ICustomerService customers,
        CurrentAccessService access,
        IToastService toasts)
    {
        _customers = customers;
        _access = access;
        _toasts = toasts;
        CustomerChangeNotifier.Changed += OnCustomersChanged;
    }

    public ObservableCollection<CustomerModel> Items { get; } = [];

    public IReadOnlyList<string> StatusFilters { get; } =
        [StatusFilterAll, "Active", "Inactive", "Blocked"];

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string selectedStatusFilter = StatusFilterAll;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isDeleting;
    [ObservableProperty] private bool hasLoadError;
    [ObservableProperty] private bool isCompact;
    [ObservableProperty] private bool canCreate;
    [ObservableProperty] private bool canUpdate;
    [ObservableProperty] private bool canDelete;
    [ObservableProperty] private int totalCount;
    [ObservableProperty] private string countLabel = "0 customers";
    [ObservableProperty] private string showingLabel = string.Empty;
    [ObservableProperty] private string rangeLabel = "1" + RangeDash + "10";

    public bool ShowLoading => IsLoading;
    public bool ShowError => HasLoadError && !IsLoading;
    public bool ShowEmpty => !IsLoading && !HasLoadError && Items.Count == 0;
    public bool ShowList => !IsLoading && !HasLoadError && Items.Count > 0;
    public bool ShowAddButton => CanCreate;
    public bool CanPressAdd => CanCreate && !IsLoading && !IsDeleting;
    public bool ShowEmptyAdd => ShowEmpty && CanCreate && !HasActiveFilter;
    public string? EmptyActionLabel => ShowEmptyAdd ? "+ Add Customer" : null;
    public bool ShowDesktopTable => ShowList && !IsCompact;
    public bool ShowMobileCards => ShowList && IsCompact;
    public bool CanRunDelete => CanDelete && !IsDeleting && !IsLoading;
    public bool HasActiveFilter =>
        !string.IsNullOrWhiteSpace(SearchText)
        || !SelectedStatusFilter.Equals(StatusFilterAll, StringComparison.OrdinalIgnoreCase);
    public string EmptyTitle => HasActiveFilter
        ? "No customers found matching your search."
        : "No customers found.";
    public string EmptySubtitle => HasActiveFilter
        ? string.Empty
        : "Customers you add for this company will appear here.";
    public bool ShowPagination => !HasLoadError && TotalCount > 0;
    public bool CanGoPrevious => !IsLoading && !IsDeleting && _skip > 0;
    public bool CanGoNext => !IsLoading && !IsDeleting && _skip + PageSize < TotalCount;

    public event EventHandler? AddRequested;
    public event EventHandler<CustomerModel>? ViewRequested;
    public event EventHandler<CustomerModel>? EditRequested;

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
            Debug.WriteLine($"[AveroNova] Customers load failed: {ex.Message}");
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
    private void View(CustomerModel? customer)
    {
        if (customer == null)
            return;
        ViewRequested?.Invoke(this, customer);
    }

    [RelayCommand]
    private void Edit(CustomerModel? customer)
    {
        if (!CanUpdate || customer == null || IsLoading || IsDeleting)
            return;
        EditRequested?.Invoke(this, customer);
    }

    [RelayCommand(CanExecute = nameof(CanRunDelete))]
    private async Task DeleteAsync(CustomerModel? customer)
    {
        if (!CanRunDelete || customer == null)
            return;

        var confirmed = await DialogHelper.ConfirmDeleteAsync(
            "Customer",
            "Are you sure you want to delete this customer?");
        if (!confirmed)
            return;

        await ExecuteDeleteAsync(customer.LocalId);
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
            var (ok, error) = await _customers.DeleteAsync(id);
            if (ok)
            {
                _toasts.ShowSuccess("Customer deleted successfully.", "The customer has been removed.");
                await LoadAsync(showLoading: false);
                return;
            }

            _toasts.ShowError(
                "Unable to delete customer.",
                string.IsNullOrWhiteSpace(error) ? "Please try again." : error);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AveroNova] Customer delete failed: {ex.Message}");
            _toasts.ShowError("Unable to delete customer.", "Please try again.");
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

    private Task<CustomerListResult> QueryPageAsync()
        => _customers.QueryAsync(new CustomerListQuery
        {
            SearchText = SearchText,
            Status = ParseStatusFilter(SelectedStatusFilter),
            Skip = _skip,
            Take = PageSize
        });

    private async Task RefreshPermissionsAsync()
    {
        var snapshot = await _access.GetSnapshotAsync();
        var manage = snapshot.Permissions.Contains(PermissionNames.CustomersManage);
        CanCreate = manage;
        CanUpdate = manage;
        CanDelete = manage;
    }

    private void OnCustomersChanged(object? sender, EventArgs e)
        => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync(showLoading: false));

    private void UpdatePagingLabels()
    {
        CountLabel = $"{TotalCount} customer{(TotalCount == 1 ? string.Empty : "s")}";
        if (TotalCount <= 0 || Items.Count == 0)
        {
            ShowingLabel = string.Empty;
            RangeLabel = "0" + RangeDash + "0";
            return;
        }

        var start = _skip + 1;
        var end = _skip + Items.Count;
        RangeLabel = $"{start}{RangeDash}{end}";
        var noun = TotalCount == 1 ? "customer" : "customers";
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

    private static CustomerStatus? ParseStatusFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals(StatusFilterAll, StringComparison.OrdinalIgnoreCase))
            return null;
        return Enum.TryParse<CustomerStatus>(value, true, out var status) ? status : null;
    }
}
