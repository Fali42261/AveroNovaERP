using System.Diagnostics;
using System.Text.RegularExpressions;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels;

public partial class CustomerFormViewModel : ObservableObject
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly ICustomerService _customers;
    private readonly CurrentAccessService _access;
    private readonly IToastService _toasts;
    private Guid? _editingId;
    private int _loadSerial;

    public CustomerFormViewModel(
        ICustomerService customers,
        CurrentAccessService access,
        IToastService toasts)
    {
        _customers = customers;
        _access = access;
        _toasts = toasts;
    }

    public IReadOnlyList<string> StatusOptions { get; } = ["Active", "Inactive", "Blocked"];

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private bool hasLoadError;
    [ObservableProperty] private bool isEditMode;
    [ObservableProperty] private bool canSaveRecord;
    [ObservableProperty] private string pageTitle = "Add Customer";

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string mobileNumber = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string address = string.Empty;
    [ObservableProperty] private string city = string.Empty;
    [ObservableProperty] private string state = string.Empty;
    [ObservableProperty] private string country = string.Empty;
    [ObservableProperty] private string pinCode = string.Empty;
    [ObservableProperty] private string taxNumber = string.Empty;
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private string selectedStatus = "Active";

    [ObservableProperty] private string nameError = string.Empty;
    [ObservableProperty] private bool hasNameError;
    [ObservableProperty] private string emailError = string.Empty;
    [ObservableProperty] private bool hasEmailError;
    [ObservableProperty] private string mobileError = string.Empty;
    [ObservableProperty] private bool hasMobileError;

    public bool ShowLoading => IsLoading;
    public bool ShowError => HasLoadError && !IsLoading;
    public bool ShowForm => !IsLoading && !HasLoadError;
    public string SaveButtonText => IsSaving ? "Saving..." : "Save";
    public bool CanPressSave => CanSaveRecord && ShowForm && !IsSaving;

    public event EventHandler? Saved;
    public event EventHandler? Cancelled;

    public async Task InitializeAsync(Guid? customerId)
    {
        var serial = ++_loadSerial;
        _editingId = customerId;
        IsEditMode = customerId.HasValue && customerId.Value != Guid.Empty;
        PageTitle = IsEditMode ? "Edit Customer" : "Add Customer";
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

            var customer = await _customers.GetByIdAsync(customerId!.Value);
            if (serial != _loadSerial)
                return;
            if (customer == null)
            {
                HasLoadError = true;
                return;
            }

            Apply(customer);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AveroNova] Customer form load failed: {ex.Message}");
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
        if (!Validate())
            return;

        IsSaving = true;
        NotifyUiState();
        try
        {
            var model = BuildModel();
            var (ok, error) = IsEditMode
                ? await _customers.UpdateAsync(model)
                : await _customers.CreateAsync(model);

            if (ok)
            {
                _toasts.ShowSuccess(
                    IsEditMode ? "Customer updated successfully." : "Customer created successfully.",
                    IsEditMode ? "The customer details have been saved." : "The customer has been saved.");
                Saved?.Invoke(this, EventArgs.Empty);
                return;
            }

            _toasts.ShowError(
                "Unable to save customer.",
                string.IsNullOrWhiteSpace(error) ? "Please try again." : error);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AveroNova] Customer save failed: {ex.Message}");
            _toasts.ShowError("Unable to save customer.", "Please try again.");
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
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private async Task RefreshPermissionsAsync()
    {
        var snapshot = await _access.GetSnapshotAsync();
        CanSaveRecord = snapshot.Permissions.Contains(PermissionNames.CustomersManage);
    }

    private void Apply(CustomerModel customer)
    {
        Name = customer.Name ?? string.Empty;
        MobileNumber = customer.Phone ?? string.Empty;
        Email = customer.Email ?? string.Empty;
        Address = customer.Address ?? string.Empty;
        City = customer.City ?? string.Empty;
        State = customer.State ?? string.Empty;
        Country = customer.Country ?? string.Empty;
        PinCode = customer.PinCode ?? string.Empty;
        TaxNumber = customer.TaxNumber ?? string.Empty;
        Notes = customer.Notes ?? string.Empty;
        SelectedStatus = customer.StatusLabel;
        ClearValidation();
    }

    private void ResetFields()
    {
        Name = MobileNumber = Email = Address = City = State = Country = PinCode = TaxNumber = Notes = string.Empty;
        SelectedStatus = "Active";
        ClearValidation();
    }

    private CustomerModel BuildModel() => new()
    {
        LocalId = _editingId ?? Guid.Empty,
        Name = Name.Trim(),
        Phone = MobileNumber.Trim(),
        Email = Email.Trim(),
        Address = Address.Trim(),
        City = City.Trim(),
        State = State.Trim(),
        Country = Country.Trim(),
        PinCode = PinCode.Trim(),
        TaxNumber = TaxNumber.Trim(),
        Notes = Notes.Trim(),
        Status = Enum.TryParse<CustomerStatus>(SelectedStatus, true, out var status)
            ? status
            : CustomerStatus.Active
    };

    private bool Validate()
    {
        ClearValidation();
        var isValid = true;

        if (string.IsNullOrWhiteSpace(Name))
        {
            NameError = "Customer name is required.";
            HasNameError = true;
            isValid = false;
        }
        else if (Name.Trim().Length > 200)
        {
            NameError = "Customer name must be 200 characters or fewer.";
            HasNameError = true;
            isValid = false;
        }

        var emailValue = Email.Trim();
        if (emailValue.Length > 0 && !EmailRegex.IsMatch(emailValue))
        {
            EmailError = "Please enter a valid email address.";
            HasEmailError = true;
            isValid = false;
        }

        var mobile = MobileNumber.Trim();
        if (mobile.Length > 15)
        {
            MobileError = "Mobile number must be 15 characters or fewer.";
            HasMobileError = true;
            isValid = false;
        }

        return isValid;
    }

    private void ClearValidation()
    {
        NameError = EmailError = MobileError = string.Empty;
        HasNameError = HasEmailError = HasMobileError = false;
    }

    private void NotifyUiState()
    {
        OnPropertyChanged(nameof(ShowLoading));
        OnPropertyChanged(nameof(ShowError));
        OnPropertyChanged(nameof(ShowForm));
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(CanPressSave));
        SaveCommand.NotifyCanExecuteChanged();
    }
}
