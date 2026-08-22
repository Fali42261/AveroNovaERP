using System.Diagnostics;
using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels;

public partial class CustomerViewViewModel : ObservableObject
{
    private readonly ICustomerService _customers;
    private readonly CurrentAccessService _access;
    private readonly IToastService _toasts;
    private CustomerModel? _loaded;
    private Guid _customerId;
    private int _loadSerial;

    public CustomerViewViewModel(
        ICustomerService customers,
        CurrentAccessService access,
        IToastService toasts)
    {
        _customers = customers;
        _access = access;
        _toasts = toasts;
    }

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isDeleting;
    [ObservableProperty] private bool hasLoadError;
    [ObservableProperty] private bool canUpdate;
    [ObservableProperty] private bool canDelete;
    [ObservableProperty] private bool isInlineLayout = true;

    [ObservableProperty] private string displayName = "—";
    [ObservableProperty] private string displayInitials = "?";
    [ObservableProperty] private string displayMobile = "—";
    [ObservableProperty] private string displayEmail = "—";
    [ObservableProperty] private string displayAddress = "—";
    [ObservableProperty] private string displayCity = "—";
    [ObservableProperty] private string displayState = "—";
    [ObservableProperty] private string displayCountry = "—";
    [ObservableProperty] private string displayPinCode = "—";
    [ObservableProperty] private string displayTaxNumber = "—";
    [ObservableProperty] private string displayNotes = "—";
    [ObservableProperty] private string displayStatus = "—";
    [ObservableProperty] private string displayCreated = "—";

    public bool ShowLoading => IsLoading;
    public bool ShowError => HasLoadError && !IsLoading;
    public bool ShowContent => !IsLoading && !HasLoadError;
    public bool ShowEditButton => ShowContent && CanUpdate;
    public bool ShowDeleteButton => ShowContent && CanDelete;
    public string DeleteButtonText => IsDeleting ? "Deleting..." : "Delete";
    public bool CanPressDelete => CanDelete && ShowContent && !IsDeleting;

    public event EventHandler? BackRequested;
    public event EventHandler<Guid>? EditRequested;
    public event EventHandler? Deleted;

    public Guid CurrentId => _customerId;

    public async Task LoadAsync(Guid customerId, bool showLoading = true)
    {
        var serial = ++_loadSerial;
        _customerId = customerId;
        if (showLoading)
            IsLoading = true;
        HasLoadError = false;
        NotifyUiState();

        try
        {
            await RefreshPermissionsAsync();
            var customer = await _customers.GetByIdAsync(customerId);
            if (serial != _loadSerial)
                return;
            if (customer == null)
            {
                HasLoadError = true;
                _loaded = null;
                return;
            }

            Apply(customer);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AveroNova] Customer details load failed: {ex.Message}");
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
    private Task RetryAsync() => LoadAsync(_customerId);

    [RelayCommand]
    private void Back() => BackRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Edit()
    {
        if (!CanUpdate || _loaded == null)
            return;
        EditRequested?.Invoke(this, _loaded.LocalId);
    }

    [RelayCommand(CanExecute = nameof(CanPressDelete))]
    private async Task DeleteAsync()
    {
        if (!CanPressDelete || _loaded == null)
            return;

        var confirmed = await DialogHelper.ConfirmDeleteAsync(
            "Customer",
            "Are you sure you want to delete this customer?");
        if (!confirmed)
            return;

        IsDeleting = true;
        NotifyUiState();
        try
        {
            var (ok, error) = await _customers.DeleteAsync(_loaded.LocalId);
            if (ok)
            {
                _toasts.ShowSuccess("Customer deleted successfully.", "The customer has been removed.");
                Deleted?.Invoke(this, EventArgs.Empty);
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
            IsDeleting = false;
            NotifyUiState();
        }
    }

    private async Task RefreshPermissionsAsync()
    {
        var snapshot = await _access.GetSnapshotAsync();
        var manage = snapshot.Permissions.Contains(PermissionNames.CustomersManage);
        CanUpdate = manage;
        CanDelete = manage;
    }

    private void Apply(CustomerModel customer)
    {
        _loaded = customer;
        DisplayName = Dash(customer.Name);
        DisplayInitials = string.IsNullOrWhiteSpace(customer.Initials) ? "?" : customer.Initials;
        DisplayMobile = Dash(customer.Phone);
        DisplayEmail = Dash(customer.Email);
        DisplayAddress = Dash(customer.Address);
        DisplayCity = Dash(customer.City);
        DisplayState = Dash(customer.State);
        DisplayCountry = Dash(customer.Country);
        DisplayPinCode = Dash(customer.PinCode);
        DisplayTaxNumber = Dash(customer.TaxNumber);
        DisplayNotes = Dash(customer.Notes);
        DisplayStatus = Dash(customer.StatusLabel);
        DisplayCreated = Dash(customer.CreatedDateLabel);
    }

    private void NotifyUiState()
    {
        OnPropertyChanged(nameof(ShowLoading));
        OnPropertyChanged(nameof(ShowError));
        OnPropertyChanged(nameof(ShowContent));
        OnPropertyChanged(nameof(ShowEditButton));
        OnPropertyChanged(nameof(ShowDeleteButton));
        OnPropertyChanged(nameof(DeleteButtonText));
        OnPropertyChanged(nameof(CanPressDelete));
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private static string Dash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
}
