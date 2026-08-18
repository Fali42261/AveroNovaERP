using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels;

public partial class RegisterPlanOption : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PriceText { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Badge { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string ActionText => IsAvailable ? "Select" : "Coming soon";

    [ObservableProperty] public partial bool IsSelected { get; set; }
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
    [ObservableProperty] public partial string PasswordEyeIcon { get; set; } = "\u25CE";
    [ObservableProperty] public partial string ConfirmPasswordEyeIcon { get; set; } = "\u25CE";
    [ObservableProperty] public partial string PasswordEyeHint { get; set; } = "Show password";
    [ObservableProperty] public partial string ConfirmPasswordEyeHint { get; set; } = "Show password";

    [ObservableProperty] public partial string SelectedPlanId { get; set; } = "starter";
    [ObservableProperty] public partial string SelectedPlanName { get; set; } = "Starter";
    [ObservableProperty] public partial string SelectedPlanSummary { get; set; } = "15-day free trial";
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

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;
    public bool IsBackVisible => CurrentStep > 1;
    public bool IsInteractionEnabled => !IsBusy;
    public string PrimaryActionText => CurrentStep >= 4
        ? (IsBusy ? "Creating account..." : "Create Account")
        : "Next";
    public string StepCaption => CurrentStep switch
    {
        1 => "Personal Information",
        2 => "Company Information",
        3 => "Subscription",
        _ => "Review & Create"
    };

    public event EventHandler? StepChanged;

    public RegisterViewModel()
    {
        ApplyEyeIcons();
        Plans.Add(new RegisterPlanOption
        {
            Id = "starter",
            Name = "Starter",
            PriceText = "₹0",
            Description = "For individuals and small teams getting started with AveroNova.",
            Badge = "15-day free trial",
            IsAvailable = true,
            IsSelected = true
        });
        Plans.Add(new RegisterPlanOption
        {
            Id = "business",
            Name = "Business",
            PriceText = "Coming soon",
            Description = "For growing businesses that need more users and companies.",
            Badge = "Coming soon",
            IsAvailable = false
        });
        Plans.Add(new RegisterPlanOption
        {
            Id = "enterprise",
            Name = "Enterprise",
            PriceText = "Coming soon",
            Description = "Custom pricing for established organizations.",
            Badge = "Coming soon",
            IsAvailable = false
        });
    }

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
        OnPropertyChanged(nameof(IsStep4));
        OnPropertyChanged(nameof(IsBackVisible));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(StepCaption));
        StepChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsInteractionEnabled));
        OnPropertyChanged(nameof(PrimaryActionText));
    }

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
        var plan = Plans.FirstOrDefault(p => p.Id == planId);
        if (plan == null || !plan.IsAvailable)
            return;

        foreach (var item in Plans)
            item.IsSelected = item.Id == plan.Id;

        SelectedPlanId = plan.Id;
        SelectedPlanName = plan.Name;
        SelectedPlanSummary = plan.Badge;
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
            1 => ValidatePersonal(),
            2 => ValidateCompany(),
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
        => ValidatePersonal() & ValidateCompany() & ValidateSubscription();

    public bool ValidatePersonal()
    {
        FullNameError = EmailError = MobileError = PersonalPinCodeError = PersonalAddressError =
            PersonalCityError = PersonalStateError = PersonalCountryError = string.Empty;
        HasFullNameError = HasEmailError = HasMobileError = HasPersonalPinCodeError =
            HasPersonalAddressError = HasPersonalCityError = HasPersonalStateError = HasPersonalCountryError = false;
        var isValid = ValidateSecurity();

        if (string.IsNullOrWhiteSpace(FullName) || FullName.Trim().Length < 2)
        {
            FullNameError = "Full name is required";
            HasFullNameError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            EmailError = "Email address is required";
            HasEmailError = true;
            isValid = false;
        }
        else if (!Regex.IsMatch(Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            EmailError = "Please enter a valid email address";
            HasEmailError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(Mobile))
        {
            MobileError = "Mobile number is required";
            HasMobileError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(PersonalPinCode))
        {
            PersonalPinCodeError = "PIN/ZIP is required";
            HasPersonalPinCodeError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(PersonalAddress))
        {
            PersonalAddressError = "Address is required";
            HasPersonalAddressError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(PersonalCity))
        {
            PersonalCityError = "City is required";
            HasPersonalCityError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(PersonalState))
        {
            PersonalStateError = "State is required";
            HasPersonalStateError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(PersonalCountry))
        {
            PersonalCountryError = "Country is required";
            HasPersonalCountryError = true;
            isValid = false;
        }

        return isValid;
    }

    public bool ValidateCompany()
    {
        CompanyNameError = OwnerNameError = GSTNumberError = PANNumberError = CompanyEmailError =
            MobileNumberError = CountryError = StateError = CityError = PinCodeError = AddressError = string.Empty;
        HasCompanyNameError = HasOwnerNameError = HasGSTNumberError = HasPANNumberError = HasCompanyEmailError =
            HasMobileNumberError = HasCountryError = HasStateError = HasCityError = HasPinCodeError = HasAddressError = false;
        var isValid = true;

        if (string.IsNullOrWhiteSpace(CompanyName) || CompanyName.Trim().Length < 2)
        {
            CompanyNameError = "Company name is required";
            HasCompanyNameError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(OwnerName))
        {
            OwnerNameError = "Owner name is required";
            HasOwnerNameError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(GSTNumber))
        {
            GSTNumberError = "GST number is required";
            HasGSTNumberError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(PANNumber))
        {
            PANNumberError = "PAN number is required";
            HasPANNumberError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(CompanyEmail))
        {
            CompanyEmailError = "Email is required";
            HasCompanyEmailError = true;
            isValid = false;
        }
        else if (!Regex.IsMatch(CompanyEmail.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            CompanyEmailError = "Please enter a valid email address";
            HasCompanyEmailError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(MobileNumber))
        {
            MobileNumberError = "Mobile number is required";
            HasMobileNumberError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(Country))
        {
            CountryError = "Country is required";
            HasCountryError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(State))
        {
            StateError = "State is required";
            HasStateError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(City))
        {
            CityError = "City is required";
            HasCityError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(PinCode))
        {
            PinCodeError = "Pin code is required";
            HasPinCodeError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(Address))
        {
            AddressError = "Address is required";
            HasAddressError = true;
            isValid = false;
        }

        return isValid;
    }

    public bool ValidateSubscription()
    {
        PlanError = string.Empty;
        HasPlanError = false;
        var selected = Plans.FirstOrDefault(p => p.Id == SelectedPlanId);
        if (selected == null || !selected.IsAvailable)
        {
            PlanError = "Select the Starter 15-day free trial to continue.";
            HasPlanError = true;
            return false;
        }

        SelectedPlanName = selected.Name;
        SelectedPlanSummary = selected.Badge;
        return true;
    }

    public bool ValidateSecurity()
    {
        PasswordError = ConfirmPasswordError = string.Empty;
        HasPasswordError = HasConfirmPasswordError = false;
        var isValid = true;

        if (string.IsNullOrWhiteSpace(Password))
        {
            PasswordError = "Password is required";
            HasPasswordError = true;
            isValid = false;
        }
        else if (Password.Length < 6)
        {
            PasswordError = "Password must be at least 6 characters";
            HasPasswordError = true;
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            ConfirmPasswordError = "Please confirm your password";
            HasConfirmPasswordError = true;
            isValid = false;
        }
        else if (Password != ConfirmPassword)
        {
            ConfirmPasswordError = "Passwords do not match";
            HasConfirmPasswordError = true;
            isValid = false;
        }

        return isValid;
    }

    public void Reset()
    {
        CurrentStep = 1;
        FullName = Email = Mobile = PersonalPinCode = PersonalAddress = PersonalCity = PersonalState =
            PersonalCountry = CompanyName = OwnerName = GSTNumber = PANNumber = CompanyEmail =
            MobileNumber = Country = State = City = PinCode = Address = Password = ConfirmPassword = string.Empty;
        SelectedPlanId = "starter";
        SelectedPlanName = "Starter";
        SelectedPlanSummary = "15-day free trial";
        foreach (var plan in Plans)
            plan.IsSelected = plan.Id == "starter";
        IsPasswordHidden = IsConfirmPasswordHidden = true;
        IsBusy = false;
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
        var eye = ResolveIcon("IconAuthEye", "\u25CE");
        var eyeOff = ResolveIcon("IconAuthEyeOff", "\u2299");
        PasswordEyeIcon = IsPasswordHidden ? eye : eyeOff;
        ConfirmPasswordEyeIcon = IsConfirmPasswordHidden ? eye : eyeOff;
        PasswordEyeHint = IsPasswordHidden ? "Show password" : "Hide password";
        ConfirmPasswordEyeHint = IsConfirmPasswordHidden ? "Show password" : "Hide password";
    }

    private static string ResolveIcon(string key, string fallback)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is string text
            && !string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return fallback;
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
    partial void OnPersonalPinCodeChanged(string value) => ClearIfFilled(value, HasPersonalPinCodeError, v => HasPersonalPinCodeError = v, v => PersonalPinCodeError = v);
    partial void OnPersonalAddressChanged(string value) => ClearIfFilled(value, HasPersonalAddressError, v => HasPersonalAddressError = v, v => PersonalAddressError = v);
    partial void OnPersonalCityChanged(string value) => ClearIfFilled(value, HasPersonalCityError, v => HasPersonalCityError = v, v => PersonalCityError = v);
    partial void OnPersonalStateChanged(string value) => ClearIfFilled(value, HasPersonalStateError, v => HasPersonalStateError = v, v => PersonalStateError = v);
    partial void OnPersonalCountryChanged(string value) => ClearIfFilled(value, HasPersonalCountryError, v => HasPersonalCountryError = v, v => PersonalCountryError = v);

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
