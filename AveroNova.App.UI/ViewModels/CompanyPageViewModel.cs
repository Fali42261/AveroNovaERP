using System.Diagnostics;
using System.Text.RegularExpressions;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels;

public partial class CompanyPageViewModel : ObservableObject
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly ICompanyService _company;
    private readonly ISubscriptionService _subscriptions;
    private readonly CurrentAccessService _access;
    private readonly IToastService _toasts;
    private CompanyModel? _loaded;
    private int _loadSerial;

    public CompanyPageViewModel(
        ICompanyService company,
        ISubscriptionService subscriptions,
        CurrentAccessService access,
        IToastService toasts)
    {
        _company = company;
        _subscriptions = subscriptions;
        _access = access;
        _toasts = toasts;
    }

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private bool hasLoadError;
    [ObservableProperty] private bool canView;
    [ObservableProperty] private bool canUpdate;

    [ObservableProperty] private string displayInitials = "?";
    [ObservableProperty] private string displayCompanyName = "—";
    [ObservableProperty] private string displayOwnerName = "—";
    [ObservableProperty] private string displayGstNumber = "—";
    [ObservableProperty] private string displayPanNumber = "—";
    [ObservableProperty] private string displayEmail = "—";
    [ObservableProperty] private string displayMobileNumber = "—";
    [ObservableProperty] private string displayAddress = "—";
    [ObservableProperty] private string displayCity = "—";
    [ObservableProperty] private string displayState = "—";
    [ObservableProperty] private string displayCountry = "—";
    [ObservableProperty] private string displayPinCode = "—";

    [ObservableProperty] private string planName = "—";
    [ObservableProperty] private string displayPrice = "—";
    [ObservableProperty] private string displayValidity = "—";
    [ObservableProperty] private string subscriptionStatus = "—";
    [ObservableProperty] private string displayStartDate = "—";
    [ObservableProperty] private string displayExpiryDate = "—";
    [ObservableProperty] private string validUntil = "—";
    [ObservableProperty] private string daysRemaining = "—";

    [ObservableProperty] private string editCompanyName = string.Empty;
    [ObservableProperty] private string editOwnerName = string.Empty;
    [ObservableProperty] private string editGstNumber = string.Empty;
    [ObservableProperty] private string editPanNumber = string.Empty;
    [ObservableProperty] private string editEmail = string.Empty;
    [ObservableProperty] private string editMobileNumber = string.Empty;
    [ObservableProperty] private string editAddress = string.Empty;
    [ObservableProperty] private string editCity = string.Empty;
    [ObservableProperty] private string editState = string.Empty;
    [ObservableProperty] private string editCountry = string.Empty;
    [ObservableProperty] private string editPinCode = string.Empty;

    [ObservableProperty] private string ownerNameError = string.Empty;
    [ObservableProperty] private bool hasOwnerNameError;
    [ObservableProperty] private string emailError = string.Empty;
    [ObservableProperty] private bool hasEmailError;
    [ObservableProperty] private string mobileNumberError = string.Empty;
    [ObservableProperty] private bool hasMobileNumberError;

    public bool ShowLoading => IsLoading;
    public bool ShowError => HasLoadError && !IsLoading;
    public bool ShowContent => !IsLoading && !HasLoadError;
    public bool ShowViewMode => ShowContent && !IsEditing;
    public bool ShowEditMode => ShowContent && IsEditing;
    public bool ShowEditButton => ShowViewMode && CanUpdate;
    public bool ShowEditFooter => ShowEditMode;
    public string SaveButtonText => IsSaving ? "Saving..." : "Save Changes";
    public bool CanPressSave => CanUpdate && IsEditing && !IsSaving;

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
                HasLoadError = true;
                _loaded = null;
                NotifyLoadFailed();
                return;
            }

            var company = await _company.GetCurrentAsync();
            if (serial != _loadSerial)
                return;

            if (company == null)
            {
                HasLoadError = true;
                _loaded = null;
                NotifyLoadFailed();
                return;
            }

            var subscription = await _subscriptions.GetCurrentAsync(company.LocalId);
            if (serial != _loadSerial)
                return;

            ApplyLoaded(company, subscription);
            if (IsEditing)
                CopyToEdit(company);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AveroNova] Company load failed: {ex.Message}");
            if (serial != _loadSerial)
                return;
            HasLoadError = true;
            _loaded = null;
            NotifyLoadFailed();
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

    [RelayCommand]
    private void BeginEdit()
    {
        if (!CanUpdate || _loaded == null || IsLoading || HasLoadError)
            return;

        CopyToEdit(_loaded);
        IsEditing = true;
        NotifyUiState();
    }

    [RelayCommand]
    private async Task CancelEditAsync()
    {
        if (IsSaving)
            return;

        ClearValidation();
        IsEditing = false;
        NotifyUiState();
        await LoadAsync(showLoading: false);
    }

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
            await RefreshPermissionsAsync();
            if (!CanUpdate)
            {
                _toasts.ShowError(
                    "Unable to update company details.",
                    "You do not have permission to update this company.");
                return;
            }

            var model = BuildUpdateModel();
            var (ok, error) = await _company.UpdateAsync(model);
            if (ok)
            {
                _toasts.ShowSuccess(
                    "Company details updated successfully.",
                    "Your company profile has been updated.");
                IsEditing = false;
                await LoadAsync(showLoading: false);
                return;
            }

            _toasts.ShowError(
                "Unable to update company details.",
                string.IsNullOrWhiteSpace(error) ? "Please try again." : error);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AveroNova] Company update failed: {ex.Message}");
            _toasts.ShowError("Unable to update company details.", "Please try again.");
        }
        finally
        {
            IsSaving = false;
            NotifyUiState();
        }
    }

    private async Task RefreshPermissionsAsync()
    {
        var snapshot = await _access.GetSnapshotAsync();
        CanView = snapshot.Permissions.Contains(PermissionNames.CompanyView);
        CanUpdate = snapshot.Permissions.Contains(PermissionNames.CompanyUpdate);
    }

    private void ApplyLoaded(CompanyModel company, SubscriptionModel? subscription)
    {
        _loaded = company;
        DisplayInitials = string.IsNullOrWhiteSpace(company.Initials) ? "?" : company.Initials;
        DisplayCompanyName = Dash(company.Name);
        DisplayOwnerName = Dash(company.OwnerName);
        DisplayGstNumber = Dash(company.TaxNumber);
        DisplayPanNumber = Dash(company.RegistrationNo);
        DisplayEmail = Dash(company.Email);
        DisplayMobileNumber = Dash(company.Phone);
        DisplayAddress = Dash(company.Address);
        DisplayCity = Dash(company.City);
        DisplayState = Dash(company.State);
        DisplayCountry = Dash(company.Country);
        DisplayPinCode = Dash(company.PinCode);
        ApplySubscription(subscription);
        CopyToEdit(company);
        ClearValidation();
    }

    private void ApplySubscription(SubscriptionModel? subscription)
    {
        if (subscription == null)
        {
            PlanName = "—";
            DisplayPrice = "—";
            DisplayValidity = "—";
            SubscriptionStatus = "—";
            DisplayStartDate = "—";
            DisplayExpiryDate = "—";
            ValidUntil = "—";
            DaysRemaining = "—";
            return;
        }

        PlanName = Dash(subscription.PlanName);
        DisplayPrice = subscription.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        DisplayValidity = FormatValidity(subscription.StartDate, subscription.ExpiryDate);
        SubscriptionStatus = Dash(subscription.StatusLabel);
        DisplayStartDate = FormatDate(subscription.StartDate);
        DisplayExpiryDate = FormatDate(subscription.ExpiryDate);
        ValidUntil = DisplayExpiryDate;
        if (subscription.ExpiryDate == default)
        {
            DaysRemaining = "—";
        }
        else
        {
            var days = subscription.DaysRemaining;
            DaysRemaining = days == 1 ? "1 Day" : $"{days} Days";
        }
    }

    private void NotifyLoadFailed()
        => _toasts.ShowError("Unable to load company details.", "Unable to load company details.");

    private void CopyToEdit(CompanyModel company)
    {
        EditCompanyName = company.Name ?? string.Empty;
        EditOwnerName = company.OwnerName ?? string.Empty;
        EditGstNumber = company.TaxNumber ?? string.Empty;
        EditPanNumber = company.RegistrationNo ?? string.Empty;
        EditEmail = company.Email ?? string.Empty;
        EditMobileNumber = company.Phone ?? string.Empty;
        EditAddress = company.Address ?? string.Empty;
        EditCity = company.City ?? string.Empty;
        EditState = company.State ?? string.Empty;
        EditCountry = company.Country ?? string.Empty;
        EditPinCode = company.PinCode ?? string.Empty;
    }

    private CompanyModel BuildUpdateModel() => new()
    {
        OwnerName = EditOwnerName.Trim(),
        TaxNumber = EditGstNumber.Trim(),
        RegistrationNo = EditPanNumber.Trim(),
        Email = EditEmail.Trim(),
        Phone = EditMobileNumber.Trim(),
        Address = EditAddress.Trim(),
        City = EditCity.Trim(),
        State = EditState.Trim(),
        Country = EditCountry.Trim(),
        PinCode = EditPinCode.Trim()
    };

    private bool Validate()
    {
        ClearValidation();
        var isValid = true;

        if (string.IsNullOrWhiteSpace(EditOwnerName))
        {
            OwnerNameError = "Owner name is required";
            HasOwnerNameError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(EditEmail))
        {
            EmailError = "Email address is required";
            HasEmailError = true;
            isValid = false;
        }
        else if (!EmailRegex.IsMatch(EditEmail.Trim()))
        {
            EmailError = "Please enter a valid email address";
            HasEmailError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(EditMobileNumber))
        {
            MobileNumberError = "Mobile number is required";
            HasMobileNumberError = true;
            isValid = false;
        }
        else if (EditMobileNumber.Trim().Length > 15)
        {
            MobileNumberError = "Mobile number must be 15 characters or fewer";
            HasMobileNumberError = true;
            isValid = false;
        }

        return isValid;
    }

    private void ClearValidation()
    {
        OwnerNameError = EmailError = MobileNumberError = string.Empty;
        HasOwnerNameError = HasEmailError = HasMobileNumberError = false;
    }

    private void NotifyUiState()
    {
        OnPropertyChanged(nameof(ShowLoading));
        OnPropertyChanged(nameof(ShowError));
        OnPropertyChanged(nameof(ShowContent));
        OnPropertyChanged(nameof(ShowViewMode));
        OnPropertyChanged(nameof(ShowEditMode));
        OnPropertyChanged(nameof(ShowEditButton));
        OnPropertyChanged(nameof(ShowEditFooter));
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(CanPressSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    private static string Dash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string FormatDate(DateTime value)
        => value == default
            ? "—"
            : value.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatValidity(DateTime start, DateTime expiry)
    {
        if (start == default || expiry == default)
            return "—";

        var days = (expiry.Date - start.Date).Days;
        if (days <= 0)
            return "—";
        return days == 1 ? "1 Day" : $"{days} Days";
    }
}
