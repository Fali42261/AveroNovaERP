using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels;

public sealed class PlanFeatureItem
{
    public string Text { get; init; } = string.Empty;
    public bool IsIncluded { get; init; }
    public bool IsPlanned => !IsIncluded;
}

public partial class RegisterPlanOption : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PriceText { get; set; } = string.Empty;
    public string PriceSupportingText { get; set; } = string.Empty;
    public string ValidityText { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Badge { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PackageSummaryHeading { get; set; } = "Package Summary";
    public string FeatureSectionHeading { get; set; } = string.Empty;
    public string InfoTitle { get; set; } = string.Empty;
    public string InfoDetail { get; set; } = string.Empty;
    public string FooterAvailabilityText { get; set; } = string.Empty;
    public IReadOnlyList<PlanFeatureItem> Features { get; set; } = [];
    public bool IsAvailable { get; set; }
    public bool IsComingSoon { get; set; }

    public bool IsSelectable => IsAvailable && !IsComingSoon;
    public bool IsLocked => !IsSelectable;
    public bool HasBadge => !string.IsNullOrWhiteSpace(Badge);
    public bool HasValidityText => !string.IsNullOrWhiteSpace(ValidityText);
    public bool HasInfoSection => !string.IsNullOrWhiteSpace(InfoTitle) || !string.IsNullOrWhiteSpace(InfoDetail);
    public bool HasPriceSupportingText => !string.IsNullOrWhiteSpace(PriceSupportingText) || HasValidityText;
    public string ActionText => IsLocked ? "Coming Soon" : IsSelected ? "Selected" : "Select";
    public string FooterActionText => ActionText;
    public string SemanticSummary => IsLocked
        ? $"{Name}, {PriceText}, {ValidityText}, coming soon, not selectable"
        : IsSelected
            ? $"{Name}, {PriceText}, {ValidityText}, selected"
            : $"{Name}, {PriceText}, {ValidityText}, available";

    [ObservableProperty] public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(ActionText));
        OnPropertyChanged(nameof(FooterActionText));
        OnPropertyChanged(nameof(SemanticSummary));
    }
}

public partial class RegisterViewModel : ObservableObject
{
    [ObservableProperty] public partial int CurrentStep { get; set; } = 1;

    [ObservableProperty] public partial string FullName { get; set; } = string.Empty;
    [ObservableProperty] public partial string Email { get; set; } = string.Empty;
    [ObservableProperty] public partial string Mobile { get; set; } = string.Empty;
    [ObservableProperty] public partial string PersonalPinCode { get; set; } = string.Empty;
    [ObservableProperty] public partial string PersonalAddress { get; set; } = string.Empty;
    [ObservableProperty] public partial string PersonalCity { get; set; } = string.Empty;
    [ObservableProperty] public partial string PersonalState { get; set; } = string.Empty;
    [ObservableProperty] public partial string PersonalCountry { get; set; } = string.Empty;
    [ObservableProperty] public partial string CompanyName { get; set; } = string.Empty;
    [ObservableProperty] public partial string OwnerName { get; set; } = string.Empty;
    [ObservableProperty] public partial string GSTNumber { get; set; } = string.Empty;
    [ObservableProperty] public partial string PANNumber { get; set; } = string.Empty;
    [ObservableProperty] public partial string CompanyEmail { get; set; } = string.Empty;
    [ObservableProperty] public partial string MobileNumber { get; set; } = string.Empty;
    [ObservableProperty] public partial string Country { get; set; } = string.Empty;
    [ObservableProperty] public partial string State { get; set; } = string.Empty;
    [ObservableProperty] public partial string City { get; set; } = string.Empty;
    [ObservableProperty] public partial string PinCode { get; set; } = string.Empty;
    [ObservableProperty] public partial string Address { get; set; } = string.Empty;
    [ObservableProperty] public partial string Password { get; set; } = string.Empty;
    [ObservableProperty] public partial string ConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty] public partial bool IsPasswordHidden { get; set; } = true;
    [ObservableProperty] public partial bool IsConfirmPasswordHidden { get; set; } = true;
    [ObservableProperty] public partial string PasswordEyeIcon { get; set; } = string.Empty;
    [ObservableProperty] public partial string ConfirmPasswordEyeIcon { get; set; } = string.Empty;
    [ObservableProperty] public partial string PasswordEyeHint { get; set; } = "Show password";
    [ObservableProperty] public partial string ConfirmPasswordEyeHint { get; set; } = "Show password";

    [ObservableProperty] public partial string SelectedPlanId { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedPlanName { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedPlanSummary { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedPlanPrice { get; set; } = "Free";
    [ObservableProperty] public partial string SelectedPlanValidity { get; set; } = "15 Days";

    public bool HasAdditionalReviewDetails
        => HasReviewValue(PersonalAddress)
           || HasReviewValue(PersonalCity)
           || HasReviewValue(PersonalState)
           || HasReviewValue(PersonalCountry)
           || HasReviewValue(PersonalPinCode);

    [ObservableProperty] public partial string PlanError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasPlanError { get; set; }

    public ObservableCollection<RegisterPlanOption> Plans { get; } = [];

    [ObservableProperty] public partial string FullNameError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasFullNameError { get; set; }
    [ObservableProperty] public partial string EmailError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasEmailError { get; set; }
    [ObservableProperty] public partial string MobileError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasMobileError { get; set; }
    [ObservableProperty] public partial string PersonalPinCodeError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasPersonalPinCodeError { get; set; }
    [ObservableProperty] public partial string PersonalAddressError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasPersonalAddressError { get; set; }
    [ObservableProperty] public partial string PersonalCityError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasPersonalCityError { get; set; }
    [ObservableProperty] public partial string PersonalStateError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasPersonalStateError { get; set; }
    [ObservableProperty] public partial string PersonalCountryError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasPersonalCountryError { get; set; }
    [ObservableProperty] public partial string CompanyNameError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasCompanyNameError { get; set; }
    [ObservableProperty] public partial string OwnerNameError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasOwnerNameError { get; set; }
    [ObservableProperty] public partial string GSTNumberError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasGSTNumberError { get; set; }
    [ObservableProperty] public partial string PANNumberError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasPANNumberError { get; set; }
    [ObservableProperty] public partial string CompanyEmailError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasCompanyEmailError { get; set; }
    [ObservableProperty] public partial string MobileNumberError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasMobileNumberError { get; set; }
    [ObservableProperty] public partial string CountryError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasCountryError { get; set; }
    [ObservableProperty] public partial string StateError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasStateError { get; set; }
    [ObservableProperty] public partial string CityError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasCityError { get; set; }
    [ObservableProperty] public partial string PinCodeError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasPinCodeError { get; set; }
    [ObservableProperty] public partial string AddressError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasAddressError { get; set; }
    [ObservableProperty] public partial string PasswordError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasPasswordError { get; set; }
    [ObservableProperty] public partial string ConfirmPasswordError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasConfirmPasswordError { get; set; }

    [ObservableProperty] public partial string GeneralError { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasGeneralError { get; set; }
    [ObservableProperty] public partial string SuccessMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasSuccessMessage { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool IsNavigating { get; set; }

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;
    public bool IsBackVisible => CurrentStep > 1;
    public bool IsInteractionEnabled => !IsBusy && !IsNavigating;
    public string PrimaryActionText => CurrentStep >= 4
        ? (IsBusy ? "Creating Account..." : "Complete Account")
        : "Next";
    public string StepCaption => CurrentStep switch
    {
        1 => "Personal Information",
        2 => "Company Information",
        3 => "Subscription",
        _ => "Review & Create"
    };

    public event EventHandler? StepChanged;

    public RegisterViewModel(ISubscriptionService subscriptions)
    {
        ApplyEyeIcons();
        LoadPlans(subscriptions);
    }

    private void LoadPlans(ISubscriptionService subscriptions)
    {
        List<SubscriptionPlanModel> catalog;
        try
        {
            catalog = subscriptions.GetPlansAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Load registration plans failed: {ex}");
            catalog = [];
        }

        if (catalog.Count == 0)
        {
            catalog =
            [
                new()
                {
                    Id = "FreeTrial",
                    Name = "Free Trial",
                    Description = "15-day Free Trial with currently available AveroNova modules.",
                    Features = ["15-day free trial"]
                }
            ];
        }

        Plans.Clear();
        foreach (var plan in RegistrationPlanCatalog.Create(catalog))
            Plans.Add(plan);

        var available = Plans.First(p => p.IsSelectable);
        available.IsSelected = true;
        ApplySelectedPlanPresentation(available);
    }

    private void ApplySelectedPlanPresentation(RegisterPlanOption plan)
    {
        SelectedPlanId = plan.Id;
        SelectedPlanName = plan.Name;
        SelectedPlanSummary = plan.Badge;
        SelectedPlanPrice = string.IsNullOrWhiteSpace(plan.PriceText) ? "Free" : plan.PriceText;
        SelectedPlanValidity = IsFreeTrialPlanId(plan.Id) ? "15 Days" : plan.Badge;
    }

    private static bool IsFreeTrialPlanId(string? id)
        => string.Equals(id, SubscriptionPlanCodes.FreeTrial, StringComparison.OrdinalIgnoreCase)
           || string.Equals(id, "FreeTrial", StringComparison.OrdinalIgnoreCase)
           || string.Equals(id, "free-trial", StringComparison.OrdinalIgnoreCase);

    private static bool HasReviewValue(string? value)
        => !string.IsNullOrWhiteSpace(value);

    private void NotifyAdditionalReviewDetails()
        => OnPropertyChanged(nameof(HasAdditionalReviewDetails));

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
        OnPropertyChanged(nameof(IsStep4));
        OnPropertyChanged(nameof(IsBackVisible));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(StepCaption));
        if (value == 4)
            OnPropertyChanged(nameof(HasAdditionalReviewDetails));
        StepChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsInteractionEnabled));
        OnPropertyChanged(nameof(PrimaryActionText));
    }

    partial void OnIsNavigatingChanged(bool value)
        => OnPropertyChanged(nameof(IsInteractionEnabled));

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordHidden = !IsPasswordHidden;
        ApplyEyeIcons();
    }

    [RelayCommand]
    private void ToggleConfirmPasswordVisibility()
    {
        IsConfirmPasswordHidden = !IsConfirmPasswordHidden;
        ApplyEyeIcons();
    }

    [RelayCommand]
    private void SelectPlan(string? planId)
    {
        if (string.IsNullOrWhiteSpace(planId))
            return;

        var plan = Plans.FirstOrDefault(p => p.Id == planId);
        if (plan == null)
            return;

        if (!plan.IsSelectable)
            return;

        foreach (var item in Plans)
            item.IsSelected = item.Id == plan.Id;

        ApplySelectedPlanPresentation(plan);
        HasPlanError = false;
        PlanError = string.Empty;
    }

    [RelayCommand]
    private void Next()
    {
        HasGeneralError = false;
        GeneralError = string.Empty;
        HasSuccessMessage = false;

        var canAdvance = CurrentStep switch
        {
            1 => ValidatePersonal(showErrors: true, markAttempted: true),
            2 => ValidateCompany(showErrors: true),
            3 => ValidateSubscription(),
            _ => false
        };

        if (canAdvance && CurrentStep < 4)
            CurrentStep++;
    }

    [RelayCommand]
    private void Back()
    {
        HasGeneralError = false;
        GeneralError = string.Empty;
        HasSuccessMessage = false;
        if (CurrentStep > 1)
            CurrentStep--;
    }

    public bool Validate()
        => ValidatePersonal(showErrors: true, markAttempted: true)
           & ValidateCompany(showErrors: true)
           & ValidateSubscription();

    public bool ValidatePersonal()
        => ValidatePersonal(showErrors: true, markAttempted: false);

    public void PrepareStep1Display()
    {
        if (CurrentStep != 1)
            return;

        ClearStep1OptionalErrors();
        ClearStep1RequiredErrors();
    }

    public void PrepareStep2Display()
    {
        if (CurrentStep != 2)
            return;

        ClearStep2OptionalErrors();
        ClearStep2RequiredErrors();
    }

    public void ValidateStep2FieldAfterInteraction(string field)
    {
        if (CurrentStep != 2)
            return;

        ClearStep2OptionalErrors();
        switch (field)
        {
            case "CompanyName":
                ValidateCompanyName(showErrors: true);
                break;
            case "OwnerName":
                ValidateOwnerName(showErrors: true);
                break;
            case "CompanyEmail":
                ValidateCompanyEmail(showErrors: true);
                break;
            case "MobileNumber":
                ValidateMobileNumber(showErrors: true);
                break;
        }
    }

    public void ValidateStep1FieldAfterInteraction(string field)
    {
        if (CurrentStep != 1)
            return;

        ClearStep1OptionalErrors();
        switch (field)
        {
            case "FullName":
                ValidateFullName(showErrors: true);
                break;
            case "Email":
                ValidateEmail(showErrors: true);
                break;
            case "Mobile":
                ValidateMobile(showErrors: true);
                break;
            case "Password":
                ValidatePassword(showErrors: true);
                if (!string.IsNullOrWhiteSpace(ConfirmPassword) || HasConfirmPasswordError)
                    ValidateConfirmPassword(showErrors: true);
                break;
            case "ConfirmPassword":
                ValidateConfirmPassword(showErrors: true);
                break;
        }
    }

    private bool _step1NextAttempted;

    public bool ValidatePersonal(bool showErrors, bool markAttempted)
    {
        if (markAttempted)
            _step1NextAttempted = true;

        FullName = StripPlaceholder(FullName, "Enter your full name");
        Email = StripPlaceholder(Email, "you@company.com", "Enter email");
        Password = StripPlaceholder(Password, "Enter password");
        ConfirmPassword = StripPlaceholder(ConfirmPassword, "Confirm password");
        Mobile = StripPlaceholder(Mobile, "Enter mobile number");
        PersonalPinCode = StripPlaceholder(PersonalPinCode, "Enter PIN or ZIP");
        PersonalAddress = StripPlaceholder(PersonalAddress, "Enter address");
        PersonalCity = StripPlaceholder(PersonalCity, "Enter city");
        PersonalState = StripPlaceholder(PersonalState, "Enter state");
        PersonalCountry = StripPlaceholder(PersonalCountry, "Enter country");

        ClearStep1OptionalErrors();

        var isValid = ValidateFullName(showErrors);
        isValid &= ValidateEmail(showErrors);
        isValid &= ValidateMobile(showErrors);
        isValid &= ValidatePassword(showErrors);
        isValid &= ValidateConfirmPassword(showErrors);
        return isValid;
    }

    private bool ValidateFullName(bool showErrors)
    {
        FullName = StripPlaceholder(FullName, "Enter your full name");
        if (string.IsNullOrWhiteSpace(FullName))
        {
            if (showErrors)
            {
                FullNameError = "Full name is required";
                HasFullNameError = true;
            }
            return false;
        }

        FullNameError = string.Empty;
        HasFullNameError = false;
        return true;
    }

    private bool ValidateEmail(bool showErrors)
    {
        Email = StripPlaceholder(Email, "you@company.com", "Enter email");
        if (string.IsNullOrWhiteSpace(Email))
        {
            if (showErrors)
            {
                EmailError = "Email address is required";
                HasEmailError = true;
            }
            return false;
        }

        if (!Regex.IsMatch(Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            if (showErrors)
            {
                EmailError = "Please enter a valid email address";
                HasEmailError = true;
            }
            return false;
        }

        EmailError = string.Empty;
        HasEmailError = false;
        return true;
    }

    private bool ValidateMobile(bool showErrors)
    {
        Mobile = StripPlaceholder(Mobile, "Enter mobile number");
        if (string.IsNullOrWhiteSpace(Mobile))
        {
            if (showErrors)
            {
                MobileError = "Mobile number is required";
                HasMobileError = true;
            }
            return false;
        }

        if (Mobile.Trim().Length > 15)
        {
            if (showErrors)
            {
                MobileError = "Mobile number must be 15 characters or fewer";
                HasMobileError = true;
            }
            return false;
        }

        MobileError = string.Empty;
        HasMobileError = false;
        return true;
    }

    private bool ValidatePassword(bool showErrors)
    {
        Password = StripPlaceholder(Password, "Enter password");
        if (string.IsNullOrWhiteSpace(Password))
        {
            if (showErrors)
            {
                PasswordError = "Password is required";
                HasPasswordError = true;
            }
            return false;
        }

        PasswordError = string.Empty;
        HasPasswordError = false;
        return true;
    }

    private bool ValidateConfirmPassword(bool showErrors)
    {
        ConfirmPassword = StripPlaceholder(ConfirmPassword, "Confirm password");
        if (string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            if (showErrors)
            {
                ConfirmPasswordError = "Please confirm your password";
                HasConfirmPasswordError = true;
            }
            return false;
        }

        if (Password != ConfirmPassword)
        {
            if (showErrors)
            {
                ConfirmPasswordError = "Passwords do not match";
                HasConfirmPasswordError = true;
            }
            return false;
        }

        ConfirmPasswordError = string.Empty;
        HasConfirmPasswordError = false;
        return true;
    }

    private void ClearStep1OptionalErrors()
    {
        PersonalPinCodeError = PersonalAddressError = PersonalCityError =
            PersonalStateError = PersonalCountryError = string.Empty;
        HasPersonalPinCodeError = HasPersonalAddressError = HasPersonalCityError =
            HasPersonalStateError = HasPersonalCountryError = false;
    }

    private void ClearStep1RequiredErrors()
    {
        FullNameError = EmailError = MobileError = PasswordError = ConfirmPasswordError = string.Empty;
        HasFullNameError = HasEmailError = HasMobileError = HasPasswordError = HasConfirmPasswordError = false;
    }

    public bool ValidateCompany()
        => ValidateCompany(showErrors: true);

    public bool ValidateCompany(bool showErrors)
    {
        CompanyName = StripPlaceholder(CompanyName, "Enter company name");
        OwnerName = StripPlaceholder(OwnerName, "Enter owner name");
        GSTNumber = StripPlaceholder(GSTNumber, "Enter GST number");
        PANNumber = StripPlaceholder(PANNumber, "Enter PAN number");
        CompanyEmail = StripPlaceholder(CompanyEmail, "Enter email", "you@company.com");
        MobileNumber = StripPlaceholder(MobileNumber, "Enter mobile number");
        Country = StripPlaceholder(Country, "Enter country");
        State = StripPlaceholder(State, "Enter state");
        City = StripPlaceholder(City, "Enter city");
        PinCode = StripPlaceholder(PinCode, "Enter pin code");
        Address = StripPlaceholder(Address, "Enter company address");

        ClearStep2OptionalErrors();

        var isValid = ValidateCompanyName(showErrors);
        isValid &= ValidateOwnerName(showErrors);
        isValid &= ValidateCompanyEmail(showErrors);
        isValid &= ValidateMobileNumber(showErrors);
        return isValid;
    }

    private bool ValidateCompanyName(bool showErrors)
    {
        CompanyName = StripPlaceholder(CompanyName, "Enter company name");
        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            if (showErrors)
            {
                CompanyNameError = "Company name is required";
                HasCompanyNameError = true;
            }
            return false;
        }

        CompanyNameError = string.Empty;
        HasCompanyNameError = false;
        return true;
    }

    private bool ValidateOwnerName(bool showErrors)
    {
        OwnerName = StripPlaceholder(OwnerName, "Enter owner name");
        if (string.IsNullOrWhiteSpace(OwnerName))
        {
            if (showErrors)
            {
                OwnerNameError = "Owner name is required";
                HasOwnerNameError = true;
            }
            return false;
        }

        OwnerNameError = string.Empty;
        HasOwnerNameError = false;
        return true;
    }

    private bool ValidateCompanyEmail(bool showErrors)
    {
        CompanyEmail = StripPlaceholder(CompanyEmail, "Enter email", "you@company.com");
        if (string.IsNullOrWhiteSpace(CompanyEmail))
        {
            if (showErrors)
            {
                CompanyEmailError = "Email address is required";
                HasCompanyEmailError = true;
            }
            return false;
        }

        if (!Regex.IsMatch(CompanyEmail.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            if (showErrors)
            {
                CompanyEmailError = "Please enter a valid email address";
                HasCompanyEmailError = true;
            }
            return false;
        }

        CompanyEmailError = string.Empty;
        HasCompanyEmailError = false;
        return true;
    }

    private bool ValidateMobileNumber(bool showErrors)
    {
        MobileNumber = StripPlaceholder(MobileNumber, "Enter mobile number");
        if (string.IsNullOrWhiteSpace(MobileNumber))
        {
            if (showErrors)
            {
                MobileNumberError = "Mobile number is required";
                HasMobileNumberError = true;
            }
            return false;
        }

        if (MobileNumber.Trim().Length > 15)
        {
            if (showErrors)
            {
                MobileNumberError = "Mobile number must be 15 characters or fewer";
                HasMobileNumberError = true;
            }
            return false;
        }

        MobileNumberError = string.Empty;
        HasMobileNumberError = false;
        return true;
    }

    private void ClearStep2OptionalErrors()
    {
        GSTNumberError = PANNumberError = CountryError = StateError =
            CityError = PinCodeError = AddressError = string.Empty;
        HasGSTNumberError = HasPANNumberError = HasCountryError = HasStateError =
            HasCityError = HasPinCodeError = HasAddressError = false;
    }

    private void ClearStep2RequiredErrors()
    {
        CompanyNameError = OwnerNameError = CompanyEmailError = MobileNumberError = string.Empty;
        HasCompanyNameError = HasOwnerNameError = HasCompanyEmailError = HasMobileNumberError = false;
    }

    public bool ValidateSecurity()
        => ValidatePassword(showErrors: true) & ValidateConfirmPassword(showErrors: true);

    public bool ValidateSubscription()
    {
        PlanError = string.Empty;
        HasPlanError = false;
        var selected = Plans.FirstOrDefault(p => p.IsSelected)
            ?? Plans.FirstOrDefault(p => p.Id == SelectedPlanId);
        if (selected == null || !selected.IsSelectable || string.IsNullOrWhiteSpace(SelectedPlanId))
        {
            PlanError = "Please select a subscription plan to continue.";
            HasPlanError = true;
            return false;
        }

        SelectedPlanId = selected.Id;
        ApplySelectedPlanPresentation(selected);
        return true;
    }

    public void Reset()
    {
        CurrentStep = 1;
        FullName = Email = Mobile = PersonalPinCode = PersonalAddress = PersonalCity = PersonalState =
            PersonalCountry = CompanyName = OwnerName = GSTNumber = PANNumber = CompanyEmail =
            MobileNumber = Country = State = City = PinCode = Address = Password = ConfirmPassword = string.Empty;
        foreach (var plan in Plans)
            plan.IsSelected = false;
        var available = Plans.FirstOrDefault(p => p.IsSelectable);
        if (available != null)
        {
            available.IsSelected = true;
            ApplySelectedPlanPresentation(available);
        }
        else
        {
            SelectedPlanId = string.Empty;
            SelectedPlanName = string.Empty;
            SelectedPlanSummary = string.Empty;
            SelectedPlanPrice = "Free";
            SelectedPlanValidity = "15 Days";
        }
        IsPasswordHidden = IsConfirmPasswordHidden = true;
        IsBusy = false;
        IsNavigating = false;
        _step1NextAttempted = false;
        ClearErrors();
        ApplyEyeIcons();
    }

    private void ClearErrors()
    {
        FullNameError = EmailError = MobileError = PersonalPinCodeError = PersonalAddressError =
            PersonalCityError = PersonalStateError = PersonalCountryError = CompanyNameError = OwnerNameError =
            GSTNumberError = PANNumberError = CompanyEmailError = MobileNumberError = CountryError = StateError =
            CityError = PinCodeError = AddressError = PasswordError = ConfirmPasswordError = PlanError =
            GeneralError = SuccessMessage = string.Empty;
        HasFullNameError = HasEmailError = HasMobileError = HasPersonalPinCodeError = HasPersonalAddressError =
            HasPersonalCityError = HasPersonalStateError = HasPersonalCountryError = HasCompanyNameError =
            HasOwnerNameError = HasGSTNumberError = HasPANNumberError = HasCompanyEmailError = HasMobileNumberError =
            HasCountryError = HasStateError = HasCityError = HasPinCodeError = HasAddressError = HasPasswordError =
            HasConfirmPasswordError = HasPlanError = HasGeneralError = HasSuccessMessage = false;
    }

    private void ApplyEyeIcons()
    {
        PasswordEyeIcon = string.Empty;
        ConfirmPasswordEyeIcon = string.Empty;
        PasswordEyeHint = IsPasswordHidden ? "Show password" : "Hide password";
        ConfirmPasswordEyeHint = IsConfirmPasswordHidden ? "Show password" : "Hide password";
    }

    partial void OnFullNameChanged(string value)
    {
        if (HasFullNameError && !string.IsNullOrWhiteSpace(value))
        {
            HasFullNameError = false;
            FullNameError = string.Empty;
        }
    }

    partial void OnEmailChanged(string value)
    {
        if (HasEmailError && !string.IsNullOrWhiteSpace(value))
        {
            HasEmailError = false;
            EmailError = string.Empty;
        }
    }

    partial void OnMobileChanged(string value) => ClearIfFilled(value, HasMobileError, v => HasMobileError = v, v => MobileError = v);
    partial void OnPersonalPinCodeChanged(string value)
    {
        ClearIfFilled(value, HasPersonalPinCodeError, v => HasPersonalPinCodeError = v, v => PersonalPinCodeError = v);
        NotifyAdditionalReviewDetails();
    }

    partial void OnPersonalAddressChanged(string value)
    {
        ClearIfFilled(value, HasPersonalAddressError, v => HasPersonalAddressError = v, v => PersonalAddressError = v);
        NotifyAdditionalReviewDetails();
    }

    partial void OnPersonalCityChanged(string value)
    {
        ClearIfFilled(value, HasPersonalCityError, v => HasPersonalCityError = v, v => PersonalCityError = v);
        NotifyAdditionalReviewDetails();
    }

    partial void OnPersonalStateChanged(string value)
    {
        ClearIfFilled(value, HasPersonalStateError, v => HasPersonalStateError = v, v => PersonalStateError = v);
        NotifyAdditionalReviewDetails();
    }

    partial void OnPersonalCountryChanged(string value)
    {
        ClearIfFilled(value, HasPersonalCountryError, v => HasPersonalCountryError = v, v => PersonalCountryError = v);
        NotifyAdditionalReviewDetails();
    }

    partial void OnCompanyNameChanged(string value)
    {
        if (HasCompanyNameError && !string.IsNullOrWhiteSpace(value))
        {
            HasCompanyNameError = false;
            CompanyNameError = string.Empty;
        }
    }

    partial void OnOwnerNameChanged(string value) => ClearIfFilled(value, HasOwnerNameError, v => HasOwnerNameError = v, v => OwnerNameError = v);
    partial void OnGSTNumberChanged(string value) => ClearIfFilled(value, HasGSTNumberError, v => HasGSTNumberError = v, v => GSTNumberError = v);
    partial void OnPANNumberChanged(string value) => ClearIfFilled(value, HasPANNumberError, v => HasPANNumberError = v, v => PANNumberError = v);
    partial void OnCompanyEmailChanged(string value) => ClearIfFilled(value, HasCompanyEmailError, v => HasCompanyEmailError = v, v => CompanyEmailError = v);
    partial void OnMobileNumberChanged(string value) => ClearIfFilled(value, HasMobileNumberError, v => HasMobileNumberError = v, v => MobileNumberError = v);
    partial void OnCountryChanged(string value) => ClearIfFilled(value, HasCountryError, v => HasCountryError = v, v => CountryError = v);
    partial void OnStateChanged(string value) => ClearIfFilled(value, HasStateError, v => HasStateError = v, v => StateError = v);
    partial void OnCityChanged(string value) => ClearIfFilled(value, HasCityError, v => HasCityError = v, v => CityError = v);
    partial void OnPinCodeChanged(string value) => ClearIfFilled(value, HasPinCodeError, v => HasPinCodeError = v, v => PinCodeError = v);
    partial void OnAddressChanged(string value) => ClearIfFilled(value, HasAddressError, v => HasAddressError = v, v => AddressError = v);

    private static string StripPlaceholder(string value, params string[] placeholders)
    {
        var text = (value ?? string.Empty).Trim();
        foreach (var placeholder in placeholders)
        {
            if (string.Equals(text, placeholder, StringComparison.OrdinalIgnoreCase))
                return string.Empty;
        }

        return text;
    }

    private static void ClearIfFilled(string value, bool hasError, Action<bool> setHas, Action<string> setError)
    {
        if (hasError && !string.IsNullOrWhiteSpace(value))
        {
            setHas(false);
            setError(string.Empty);
        }
    }

    partial void OnPasswordChanged(string value)
    {
        if (HasPasswordError && !string.IsNullOrWhiteSpace(value))
        {
            HasPasswordError = false;
            PasswordError = string.Empty;
        }
    }

    partial void OnConfirmPasswordChanged(string value)
    {
        if (HasConfirmPasswordError && value == Password)
        {
            HasConfirmPasswordError = false;
            ConfirmPasswordError = string.Empty;
        }
    }
}
