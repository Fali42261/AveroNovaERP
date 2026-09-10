using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Pages.Authentication;
using AveroNova.App.UI.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels
{
    public partial class RegisterPlanOption : ObservableObject
    {
        public RegisterPlanOption(SubscriptionPlanModel plan)
        {
            Plan = plan;
        }

        public SubscriptionPlanModel Plan { get; }

        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        public string Name => Plan.Name;
        public IReadOnlyList<string> Features => Plan.Features;
        public bool IsPopular => Plan.IsPopular;

        public bool IsAvailable => Plan.IsAvailable;

        public bool HasCustomPricing =>
            Plan.MonthlyPrice == 0 && Plan.YearlyPrice == 0 &&
            !string.Equals(Plan.Id, "starter", StringComparison.OrdinalIgnoreCase);

        public string PriceDisplay
        {
            get
            {
                if (HasCustomPricing)
                    return "Custom Pricing";
                if (Plan.MonthlyPrice == 0 && Plan.YearlyPrice == 0)
                    return "Free";
                return FormatInr(Plan.MonthlyPrice);
            }
        }

        public string BillingPeriodDisplay
        {
            get
            {
                if (HasCustomPricing)
                    return "Contact Us";
                if (Plan.MonthlyPrice == 0 && Plan.YearlyPrice == 0)
                    return "No billing";
                if (Plan.YearlyPrice > 0)
                    return $"Monthly · {FormatInr(Plan.YearlyPrice)}/year";
                return "Monthly";
            }
        }

        public bool HasTrial => Plan.TrialDays > 0;

        public string TrialDisplay => HasTrial ? $"{Plan.TrialDays} Days" : string.Empty;

        public string TrialHeadline =>
            string.Equals(Plan.Id, "starter", StringComparison.OrdinalIgnoreCase)
                ? "15 Days Free Trial"
                : TrialDisplay;

        public string Details => Plan.Description;

        public string Summary
        {
            get
            {
                var users = Plan.MaxUsers < 0 ? "Unlimited users" : $"{Plan.MaxUsers} users";
                var companies = Plan.MaxCompanies < 0 ? "unlimited companies" : $"{Plan.MaxCompanies} {(Plan.MaxCompanies == 1 ? "company" : "companies")}";
                return $"{users} · {companies}";
            }
        }

        public string SelectButtonText
        {
            get
            {
                if (!IsAvailable)
                    return "Coming Soon";
                if (IsSelected)
                    return "Selected";
                if (string.Equals(Plan.Id, "starter", StringComparison.OrdinalIgnoreCase))
                    return "Start 15-Day Free Trial";
                return "Select";
            }
        }

        private static string FormatInr(decimal amount) => $"₹{amount:N2}";

        partial void OnIsSelectedChanged(bool value)
        {
            OnPropertyChanged(nameof(SelectButtonText));
        }
    }

    public partial class RegisterViewModel : ObservableObject
    {
        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private readonly ISubscriptionService? _subscriptions;

        [ObservableProperty] public partial int CurrentStep { get; set; } = 1;

        // ── Step 1 — Personal (FullName replaces FirstName + LastName) ────────
        [ObservableProperty] public partial string FullName { get; set; } = string.Empty;
        [ObservableProperty] public partial string Email { get; set; } = string.Empty;
        [ObservableProperty] public partial string Mobile { get; set; } = string.Empty;
        [ObservableProperty] public partial string Address { get; set; } = string.Empty;
        [ObservableProperty] public partial string City { get; set; } = string.Empty;
        [ObservableProperty] public partial string State { get; set; } = string.Empty;
        [ObservableProperty] public partial string Country { get; set; } = string.Empty;
        [ObservableProperty] public partial string PinCode { get; set; } = string.Empty;

        // Existing registration credentials (RegisterAsync)
        [ObservableProperty] public partial string Password { get; set; } = string.Empty;
        [ObservableProperty] public partial string ConfirmPassword { get; set; } = string.Empty;
        [ObservableProperty] public partial bool IsPasswordHidden { get; set; } = true;
        [ObservableProperty] public partial bool IsConfirmPasswordHidden { get; set; } = true;
        [ObservableProperty] public partial string ShowPasswordText { get; set; } = "Show";
        [ObservableProperty] public partial string ShowConfirmPasswordText { get; set; } = "Show";

        // ── Step 2 — Company (Company entity / CompanySetupViewModel) ─────────
        [ObservableProperty] public partial string CompanyName { get; set; } = string.Empty;
        [ObservableProperty] public partial string OwnerName { get; set; } = string.Empty;
        [ObservableProperty] public partial string GSTNumber { get; set; } = string.Empty;
        [ObservableProperty] public partial string PanNumber { get; set; } = string.Empty;
        [ObservableProperty] public partial string CompanyEmail { get; set; } = string.Empty;
        [ObservableProperty] public partial string CompanyMobile { get; set; } = string.Empty;
        [ObservableProperty] public partial string CompanyCountry { get; set; } = string.Empty;
        [ObservableProperty] public partial string CompanyState { get; set; } = string.Empty;
        [ObservableProperty] public partial string CompanyCity { get; set; } = string.Empty;
        [ObservableProperty] public partial string CompanyPinCode { get; set; } = string.Empty;
        [ObservableProperty] public partial string CompanyAddress { get; set; } = string.Empty;

        // ── Step 3 — Subscription (existing ISubscriptionService plans) ───────
        [ObservableProperty] public partial ObservableCollection<RegisterPlanOption> PlanOptions { get; set; } = [];
        [ObservableProperty] public partial RegisterPlanOption? SelectedPlanOption { get; set; }

        // ── Errors ────────────────────────────────────────────────────────────
        [ObservableProperty] public partial string FullNameError { get; set; } = string.Empty;
        [ObservableProperty] public partial string EmailError { get; set; } = string.Empty;
        [ObservableProperty] public partial string MobileError { get; set; } = string.Empty;
        [ObservableProperty] public partial string AddressError { get; set; } = string.Empty;
        [ObservableProperty] public partial string CityError { get; set; } = string.Empty;
        [ObservableProperty] public partial string StateError { get; set; } = string.Empty;
        [ObservableProperty] public partial string CountryError { get; set; } = string.Empty;
        [ObservableProperty] public partial string PinCodeError { get; set; } = string.Empty;
        [ObservableProperty] public partial string PasswordError { get; set; } = string.Empty;
        [ObservableProperty] public partial string ConfirmPasswordError { get; set; } = string.Empty;

        [ObservableProperty] public partial string CompanyNameError { get; set; } = string.Empty;
        [ObservableProperty] public partial string OwnerNameError { get; set; } = string.Empty;
        [ObservableProperty] public partial string GSTNumberError { get; set; } = string.Empty;
        [ObservableProperty] public partial string PanNumberError { get; set; } = string.Empty;
        [ObservableProperty] public partial string CompanyEmailError { get; set; } = string.Empty;
        [ObservableProperty] public partial string CompanyMobileError { get; set; } = string.Empty;
        [ObservableProperty] public partial string CompanyCountryError { get; set; } = string.Empty;
        [ObservableProperty] public partial string CompanyStateError { get; set; } = string.Empty;
        [ObservableProperty] public partial string CompanyCityError { get; set; } = string.Empty;
        [ObservableProperty] public partial string CompanyPinCodeError { get; set; } = string.Empty;
        [ObservableProperty] public partial string CompanyAddressError { get; set; } = string.Empty;

        [ObservableProperty] public partial string SubscriptionError { get; set; } = string.Empty;

        [ObservableProperty] public partial bool HasFullNameError { get; set; }
        [ObservableProperty] public partial bool HasEmailError { get; set; }
        [ObservableProperty] public partial bool HasMobileError { get; set; }
        [ObservableProperty] public partial bool HasAddressError { get; set; }
        [ObservableProperty] public partial bool HasCityError { get; set; }
        [ObservableProperty] public partial bool HasStateError { get; set; }
        [ObservableProperty] public partial bool HasCountryError { get; set; }
        [ObservableProperty] public partial bool HasPinCodeError { get; set; }
        [ObservableProperty] public partial bool HasPasswordError { get; set; }
        [ObservableProperty] public partial bool HasConfirmPasswordError { get; set; }

        [ObservableProperty] public partial bool HasCompanyNameError { get; set; }
        [ObservableProperty] public partial bool HasOwnerNameError { get; set; }
        [ObservableProperty] public partial bool HasGSTNumberError { get; set; }
        [ObservableProperty] public partial bool HasPanNumberError { get; set; }
        [ObservableProperty] public partial bool HasCompanyEmailError { get; set; }
        [ObservableProperty] public partial bool HasCompanyMobileError { get; set; }
        [ObservableProperty] public partial bool HasCompanyCountryError { get; set; }
        [ObservableProperty] public partial bool HasCompanyStateError { get; set; }
        [ObservableProperty] public partial bool HasCompanyCityError { get; set; }
        [ObservableProperty] public partial bool HasCompanyPinCodeError { get; set; }
        [ObservableProperty] public partial bool HasCompanyAddressError { get; set; }

        [ObservableProperty] public partial bool HasSubscriptionError { get; set; }
        [ObservableProperty] public partial bool HasGeneralError { get; set; }
        [ObservableProperty] public partial bool HasGeneralSuccess { get; set; }
        [ObservableProperty] public partial string GeneralError { get; set; } = string.Empty;
        [ObservableProperty] public partial string GeneralSuccess { get; set; } = string.Empty;
        [ObservableProperty] public partial bool IsBusy { get; set; }
        [ObservableProperty] public partial string BusyText { get; set; } = "Creating account...";

        public bool IsStep1 => CurrentStep == 1;
        public bool IsStep2 => CurrentStep == 2;
        public bool IsStep3 => CurrentStep == 3;
        public bool IsStep4 => CurrentStep == 4;
        public bool CanGoBack => CurrentStep > 1;
        public bool ShowLoginLink => CurrentStep == 1;
        public bool IsInteractionEnabled => !IsBusy;
        public string PrimaryActionText => CurrentStep == 4 ? "Submit" : "Next";
        public string StepTitle => CurrentStep switch
        {
            1 => "Personal Information",
            2 => "Company Details",
            3 => "Subscription",
            4 => "Review & Create",
            _ => "Create Account"
        };

        public string SelectedPlanName => SelectedPlanOption?.Name ?? "—";
        public string SelectedPlanPrice => SelectedPlanOption?.PriceDisplay ?? "—";
        public string SelectedBillingPeriod => SelectedPlanOption?.BillingPeriodDisplay ?? "—";
        public string SelectedPlanUsers => FormatLimit(SelectedPlanOption?.Plan.MaxUsers);
        public string SelectedPlanCompanies => FormatLimit(SelectedPlanOption?.Plan.MaxCompanies);
        public string SelectedTrialInfo => SelectedPlanOption is { HasTrial: true } p ? p.TrialDisplay : string.Empty;
        public bool HasSelectedTrial => !string.IsNullOrWhiteSpace(SelectedTrialInfo);

        private static string FormatLimit(int? value)
        {
            if (value is null)
                return "—";
            return value < 0 ? "Unlimited" : value.Value.ToString();
        }

        public RegisterViewModel()
        {
        }

        public RegisterViewModel(ISubscriptionService subscriptions)
        {
            _subscriptions = subscriptions;
            _ = LoadPlansAsync();
        }

        public async Task LoadPlansAsync()
        {
            if (_subscriptions == null)
                return;

            var plans = await _subscriptions.GetPlansAsync();
            var order = new[] { "starter", "business", "enterprise" };
            var options = order
                .Select(id => plans.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)))
                .OfType<SubscriptionPlanModel>()
                .Select(p => new RegisterPlanOption(p))
                .ToList();

            var previousId = SelectedPlanOption?.Plan.Id;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PlanOptions = new ObservableCollection<RegisterPlanOption>(options);
                if (!string.IsNullOrEmpty(previousId))
                {
                    var match = PlanOptions.FirstOrDefault(p => p.Plan.Id == previousId && p.IsAvailable);
                    if (match != null)
                        ApplyPlanSelection(match);
                }
            });
        }

        [RelayCommand]
        private void SelectPlan(RegisterPlanOption? option)
        {
            if (option == null || !option.IsAvailable)
                return;
            ApplyPlanSelection(option);
        }

        private void ApplyPlanSelection(RegisterPlanOption option)
        {
            foreach (var plan in PlanOptions)
                plan.IsSelected = ReferenceEquals(plan, option);

            SelectedPlanOption = option;
            HasSubscriptionError = false;
            SubscriptionError = string.Empty;
            NotifyReviewSubscription();
        }

        [RelayCommand]
        private void TogglePasswordVisibility()
        {
            IsPasswordHidden = !IsPasswordHidden;
            ShowPasswordText = IsPasswordHidden ? "Show" : "Hide";
        }

        [RelayCommand]
        private void ToggleConfirmPasswordVisibility()
        {
            IsConfirmPasswordHidden = !IsConfirmPasswordHidden;
            ShowConfirmPasswordText = IsConfirmPasswordHidden ? "Show" : "Hide";
        }

        [RelayCommand]
        private void Next()
        {
            if (!ValidateCurrentStep())
                return;

            if (CurrentStep < 4)
                CurrentStep++;
        }

        [RelayCommand]
        private void Back()
        {
            if (CurrentStep > 1)
                CurrentStep--;
        }

        [RelayCommand]
        private async Task RegisterAsync()
        {
            if (!ValidateForm())
                return;

            IsBusy = true;
            HasGeneralError = false;
            GeneralError = string.Empty;
            HasGeneralSuccess = false;
            GeneralSuccess = string.Empty;

            try
            {
                await Task.Delay(1000);
                HasGeneralSuccess = true;
                GeneralSuccess = "Account created successfully! Redirecting to sign in...";
                await Task.Delay(1200);
                await NavigateToLoginAsync();
            }
            catch (Exception ex)
            {
                HasGeneralError = true;
                GeneralError = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task NavigateToLoginAsync()
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(LoginPage));
            }
            catch
            {
                if (Microsoft.Maui.Controls.Application.Current?.Windows.Count > 0 &&
                    Microsoft.Maui.Controls.Application.Current.Windows[0].Page?.Navigation != null)
                {
                    await Shell.Current.GoToAsync("//LoginPage");
                }
            }
        }

        public bool ValidateCurrentStep()
        {
            return CurrentStep switch
            {
                1 => ValidatePersonal(),
                2 => ValidateCompany(),
                3 => ValidateSubscription(),
                4 => ValidateForm(),
                _ => true
            };
        }

        public bool ValidateForm()
        {
            var personal = ValidatePersonal();
            var company = ValidateCompany();
            var subscription = ValidateSubscription();
            return personal && company && subscription;
        }

        public bool ValidatePersonal()
        {
            var isValid = true;
            ClearPersonalErrors();

            if (string.IsNullOrWhiteSpace(FullName) || FullName.Trim().Length < 2)
            {
                FullNameError = string.IsNullOrWhiteSpace(FullName) ? "Full Name is required" : "Full Name must be at least 2 characters";
                HasFullNameError = true;
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(Email))
            {
                EmailError = "Email is required";
                HasEmailError = true;
                isValid = false;
            }
            else if (!EmailRegex.IsMatch(Email.Trim()))
            {
                EmailError = "Please enter a valid email address";
                HasEmailError = true;
                isValid = false;
            }

            if (!TryValidateMobile(Mobile, required: true, out var mobileError))
            {
                MobileError = mobileError;
                HasMobileError = true;
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                PasswordError = "Password is required";
                HasPasswordError = true;
                isValid = false;
            }
            else if (!AveroNova.Shared.Security.PasswordPolicy.IsStrong(Password))
            {
                PasswordError = AveroNova.Shared.Security.PasswordPolicy.RequirementMessage;
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

        public bool ValidateCompany()
        {
            var isValid = true;
            ClearCompanyErrors();

            void Require(string value, Action<string> setError, Action<bool> setHas, string message)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    setError(message);
                    setHas(true);
                    isValid = false;
                }
            }

            Require(CompanyName, v => CompanyNameError = v, v => HasCompanyNameError = v, "Company Name is required");
            Require(OwnerName, v => OwnerNameError = v, v => HasOwnerNameError = v, "Owner Name is required");

            if (string.IsNullOrWhiteSpace(CompanyEmail))
            {
                CompanyEmailError = "Email is required";
                HasCompanyEmailError = true;
                isValid = false;
            }
            else if (!EmailRegex.IsMatch(CompanyEmail.Trim()))
            {
                CompanyEmailError = "Please enter a valid email address";
                HasCompanyEmailError = true;
                isValid = false;
            }

            if (!TryValidateMobile(CompanyMobile, required: true, out var mobileError))
            {
                CompanyMobileError = mobileError;
                HasCompanyMobileError = true;
                isValid = false;
            }

            return isValid;
        }

        public bool ValidateSubscription()
        {
            if (SelectedPlanOption == null || !SelectedPlanOption.IsAvailable)
            {
                SubscriptionError = "Please select a subscription plan";
                HasSubscriptionError = true;
                return false;
            }

            HasSubscriptionError = false;
            SubscriptionError = string.Empty;
            return true;
        }

        private static bool TryValidateMobile(string? value, bool required, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                if (required)
                {
                    error = "Mobile Number is required";
                    return false;
                }
                return true;
            }

            var cleaned = Regex.Replace(value.Trim(), @"[\s\-\(\)\+]", "");
            if (cleaned.Length < 7 || cleaned.Length > 15 || !Regex.IsMatch(cleaned, @"^\d+$"))
            {
                error = "Please enter a valid mobile number";
                return false;
            }

            return true;
        }

        private void ClearPersonalErrors()
        {
            FullNameError = EmailError = MobileError =
                AddressError = CityError = StateError = CountryError = PinCodeError =
                PasswordError = ConfirmPasswordError = string.Empty;
            HasFullNameError = HasEmailError = HasMobileError =
                HasAddressError = HasCityError = HasStateError = HasCountryError = HasPinCodeError =
                HasPasswordError = HasConfirmPasswordError = false;
            HasGeneralError = false;
            GeneralError = string.Empty;
            HasGeneralSuccess = false;
            GeneralSuccess = string.Empty;
        }

        private void ClearCompanyErrors()
        {
            CompanyNameError = OwnerNameError = GSTNumberError = PanNumberError =
                CompanyEmailError = CompanyMobileError = CompanyCountryError = CompanyStateError =
                CompanyCityError = CompanyPinCodeError = CompanyAddressError = string.Empty;
            HasCompanyNameError = HasOwnerNameError = HasGSTNumberError = HasPanNumberError =
                HasCompanyEmailError = HasCompanyMobileError = HasCompanyCountryError = HasCompanyStateError =
                HasCompanyCityError = HasCompanyPinCodeError = HasCompanyAddressError = false;
        }

        partial void OnCurrentStepChanged(int value)
        {
            OnPropertyChanged(nameof(IsStep1));
            OnPropertyChanged(nameof(IsStep2));
            OnPropertyChanged(nameof(IsStep3));
            OnPropertyChanged(nameof(IsStep4));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(ShowLoginLink));
            OnPropertyChanged(nameof(PrimaryActionText));
            OnPropertyChanged(nameof(StepTitle));
            if (value == 4)
                NotifyReviewSubscription();
        }

        partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsInteractionEnabled));

        private void NotifyReviewSubscription()
        {
            OnPropertyChanged(nameof(SelectedPlanName));
            OnPropertyChanged(nameof(SelectedPlanPrice));
            OnPropertyChanged(nameof(SelectedBillingPeriod));
            OnPropertyChanged(nameof(SelectedPlanUsers));
            OnPropertyChanged(nameof(SelectedPlanCompanies));
            OnPropertyChanged(nameof(SelectedTrialInfo));
            OnPropertyChanged(nameof(HasSelectedTrial));
        }

        partial void OnFullNameChanged(string value) { if (HasFullNameError && !string.IsNullOrWhiteSpace(value)) { HasFullNameError = false; FullNameError = string.Empty; } }
        partial void OnEmailChanged(string value) { if (HasEmailError && !string.IsNullOrWhiteSpace(value)) { HasEmailError = false; EmailError = string.Empty; } }
        partial void OnMobileChanged(string value) { if (HasMobileError && !string.IsNullOrWhiteSpace(value)) { HasMobileError = false; MobileError = string.Empty; } }
        partial void OnAddressChanged(string value) { if (HasAddressError && !string.IsNullOrWhiteSpace(value)) { HasAddressError = false; AddressError = string.Empty; } }
        partial void OnCityChanged(string value) { if (HasCityError && !string.IsNullOrWhiteSpace(value)) { HasCityError = false; CityError = string.Empty; } }
        partial void OnStateChanged(string value) { if (HasStateError && !string.IsNullOrWhiteSpace(value)) { HasStateError = false; StateError = string.Empty; } }
        partial void OnCountryChanged(string value) { if (HasCountryError && !string.IsNullOrWhiteSpace(value)) { HasCountryError = false; CountryError = string.Empty; } }
        partial void OnPinCodeChanged(string value) { if (HasPinCodeError && !string.IsNullOrWhiteSpace(value)) { HasPinCodeError = false; PinCodeError = string.Empty; } }
        partial void OnPasswordChanged(string value)
        {
            if (HasPasswordError && !string.IsNullOrWhiteSpace(value)) { HasPasswordError = false; PasswordError = string.Empty; }
            if (HasConfirmPasswordError && !string.IsNullOrWhiteSpace(ConfirmPassword) && value == ConfirmPassword)
            { HasConfirmPasswordError = false; ConfirmPasswordError = string.Empty; }
        }
        partial void OnConfirmPasswordChanged(string value)
        {
            if (HasConfirmPasswordError && !string.IsNullOrWhiteSpace(value) && value == Password)
            { HasConfirmPasswordError = false; ConfirmPasswordError = string.Empty; }
        }
        partial void OnCompanyNameChanged(string value) { if (HasCompanyNameError && !string.IsNullOrWhiteSpace(value)) { HasCompanyNameError = false; CompanyNameError = string.Empty; } }
        partial void OnOwnerNameChanged(string value) { if (HasOwnerNameError && !string.IsNullOrWhiteSpace(value)) { HasOwnerNameError = false; OwnerNameError = string.Empty; } }
        partial void OnGSTNumberChanged(string value) { if (HasGSTNumberError && !string.IsNullOrWhiteSpace(value)) { HasGSTNumberError = false; GSTNumberError = string.Empty; } }
        partial void OnPanNumberChanged(string value) { if (HasPanNumberError && !string.IsNullOrWhiteSpace(value)) { HasPanNumberError = false; PanNumberError = string.Empty; } }
        partial void OnCompanyEmailChanged(string value) { if (HasCompanyEmailError && !string.IsNullOrWhiteSpace(value)) { HasCompanyEmailError = false; CompanyEmailError = string.Empty; } }
        partial void OnCompanyMobileChanged(string value) { if (HasCompanyMobileError && !string.IsNullOrWhiteSpace(value)) { HasCompanyMobileError = false; CompanyMobileError = string.Empty; } }
        partial void OnCompanyCountryChanged(string value) { if (HasCompanyCountryError && !string.IsNullOrWhiteSpace(value)) { HasCompanyCountryError = false; CompanyCountryError = string.Empty; } }
        partial void OnCompanyStateChanged(string value) { if (HasCompanyStateError && !string.IsNullOrWhiteSpace(value)) { HasCompanyStateError = false; CompanyStateError = string.Empty; } }
        partial void OnCompanyCityChanged(string value) { if (HasCompanyCityError && !string.IsNullOrWhiteSpace(value)) { HasCompanyCityError = false; CompanyCityError = string.Empty; } }
        partial void OnCompanyPinCodeChanged(string value) { if (HasCompanyPinCodeError && !string.IsNullOrWhiteSpace(value)) { HasCompanyPinCodeError = false; CompanyPinCodeError = string.Empty; } }
        partial void OnCompanyAddressChanged(string value) { if (HasCompanyAddressError && !string.IsNullOrWhiteSpace(value)) { HasCompanyAddressError = false; CompanyAddressError = string.Empty; } }
    }
}
