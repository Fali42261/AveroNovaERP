using System.Text.RegularExpressions;
using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels
{
    /// <summary>
    /// Manages the entire multi-step registration flow.
    /// Step 1: Welcome
    /// Step 2: User Details (First Name, Last Name, Email, Phone, Password, Confirm Password)
    /// Step 3: Company Details (Company Name, Business Type, Address, City, State, GSTIN)
    /// Step 4: Subscription (Plan selection, trial dates)
    /// Step 5: Review (Review all details)
    /// Step 6: Create Account (Submission)
    /// Step 7: Success → Navigate to Login
    /// </summary>
    public partial class RegistrationWizardViewModel : ObservableObject
    {
        // ══════════════════════════════════════════════════════════════
        // STEP TRACKING
        // ══════════════════════════════════════════════════════════════

        [ObservableProperty]
        public partial int CurrentStep { get; set; } = 1;

        // ══════════════════════════════════════════════════════════════
        // STEP 2 — USER DETAILS
        // ══════════════════════════════════════════════════════════════

        [ObservableProperty]
        public partial string FirstName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string LastName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Email { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Phone { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Password { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ConfirmPassword { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsPasswordHidden { get; set; } = true;

        [ObservableProperty]
        public partial bool IsConfirmPasswordHidden { get; set; } = true;

        [ObservableProperty]
        public partial string ShowPasswordText { get; set; } = "Show";

        [ObservableProperty]
        public partial string ShowConfirmPasswordText { get; set; } = "Show";

        // Step 2 Errors
        [ObservableProperty]
        public partial string FirstNameError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasFirstNameError { get; set; }

        [ObservableProperty]
        public partial string LastNameError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasLastNameError { get; set; }

        [ObservableProperty]
        public partial string EmailError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasEmailError { get; set; }

        [ObservableProperty]
        public partial string PhoneError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasPhoneError { get; set; }

        [ObservableProperty]
        public partial string PasswordError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasPasswordError { get; set; }

        [ObservableProperty]
        public partial string ConfirmPasswordError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasConfirmPasswordError { get; set; }

        // ══════════════════════════════════════════════════════════════
        // STEP 3 — COMPANY DETAILS
        // ══════════════════════════════════════════════════════════════

        [ObservableProperty]
        public partial string CompanyName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string BusinessType { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Address { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string City { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string State { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Gstin { get; set; } = string.Empty;

        // Step 3 Errors
        [ObservableProperty]
        public partial string CompanyNameError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasCompanyNameError { get; set; }

        [ObservableProperty]
        public partial string BusinessTypeError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasBusinessTypeError { get; set; }

        [ObservableProperty]
        public partial string AddressError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasAddressError { get; set; }

        [ObservableProperty]
        public partial string CityError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasCityError { get; set; }

        [ObservableProperty]
        public partial string StateError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasStateError { get; set; }

        // ══════════════════════════════════════════════════════════════
        // STEP 4 — SUBSCRIPTION
        // ══════════════════════════════════════════════════════════════

        [ObservableProperty]
        public partial string SelectedPlan { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string PlanDescription { get; set; } = string.Empty;

        [ObservableProperty]
        public partial decimal PlanPrice { get; set; }

        [ObservableProperty]
        public partial int TrialDays { get; set; }

        [ObservableProperty]
        public partial DateTime TrialStartDate { get; set; } = DateTime.UtcNow.Date;

        [ObservableProperty]
        public partial DateTime TrialEndDate { get; set; } = DateTime.UtcNow.Date;

        [ObservableProperty]
        public partial int CreditLimit { get; set; }

        [ObservableProperty]
        public partial int CreditsUsed { get; set; }

        [ObservableProperty]
        public partial int RemainingCredits { get; set; }

        private readonly IAuthenticationService _auth;

        // ══════════════════════════════════════════════════════════════
        // GENERAL STATE
        // ══════════════════════════════════════════════════════════════

        [ObservableProperty]
        public partial string GeneralError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasGeneralError { get; set; }

        [ObservableProperty]
        public partial string GeneralSuccess { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasGeneralSuccess { get; set; }

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        public partial string BusyText { get; set; } = "Loading...";

        public string NextButtonText => CurrentStep >= 5
            ? (IsBusy ? "Loading..." : "Create Account")
            : (IsBusy ? "Loading..." : "Next");

        public RegistrationWizardViewModel(IAuthenticationService auth)
        {
            _auth = auth;
            ApplyPlanConfiguration();
        }

        public void BeginNewCompanyRegistration()
        {
            ResetForm();
            CurrentStep = 2;
            ApplyPlanConfiguration();
        }

        public void ResetForm()
        {
            FirstName = LastName = Email = Phone = Password = ConfirmPassword = string.Empty;
            CompanyName = BusinessType = Address = City = State = Gstin = string.Empty;
            HasFirstNameError = HasEmailError = HasPasswordError = HasConfirmPasswordError = false;
            HasCompanyNameError = HasGeneralError = HasGeneralSuccess = false;
            IsBusy = false;
            CurrentStep = 1;
        }

        // ══════════════════════════════════════════════════════════════
        // NAVIGATION COMMANDS
        // ══════════════════════════════════════════════════════════════

        [RelayCommand(CanExecute = nameof(CanGoNext))]
        public async Task NextAsync()
        {
            HasGeneralError = false;
            GeneralError = string.Empty;

            if (CurrentStep == 1)
            {
                CurrentStep = 2;
            }
            else if (CurrentStep == 2)
            {
                if (!ValidateStep2())
                    return;
                CurrentStep = 3;
            }
            else if (CurrentStep == 3)
            {
                if (!ValidateStep3())
                    return;
                CurrentStep = 4;
            }
            else if (CurrentStep == 4)
            {
                ApplyPlanConfiguration();
                CurrentStep = 5;
            }
            else if (CurrentStep == 5)
            {
                await SubmitRegistrationAsync();
            }
        }

        private bool CanGoNext() => !IsBusy;

        partial void OnIsBusyChanged(bool value)
        {
            NextCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(NextButtonText));
        }

        partial void OnCurrentStepChanged(int value)
            => OnPropertyChanged(nameof(NextButtonText));

        [RelayCommand]
        public void Back()
        {
            if (CurrentStep > 1)
            {
                CurrentStep--;
                HasGeneralError = false;
                GeneralError = string.Empty;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // PASSWORD VISIBILITY TOGGLES
        // ══════════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════════
        // VALIDATION
        // ══════════════════════════════════════════════════════════════

        private bool ValidateStep2()
        {
            bool isValid = true;

            // Clear previous errors
            FirstNameError = string.Empty;
            HasFirstNameError = false;

            LastNameError = string.Empty;
            HasLastNameError = false;

            EmailError = string.Empty;
            HasEmailError = false;

            PhoneError = string.Empty;
            HasPhoneError = false;

            PasswordError = string.Empty;
            HasPasswordError = false;

            ConfirmPasswordError = string.Empty;
            HasConfirmPasswordError = false;

            // 1. First Name validation
            if (string.IsNullOrWhiteSpace(FirstName))
            {
                FirstNameError = "Full name is required";
                HasFirstNameError = true;
                isValid = false;
            }
            else if (FirstName.Trim().Length < 2)
            {
                FirstNameError = "Full name must be at least 2 characters";
                HasFirstNameError = true;
                isValid = false;
            }

            // 5. Last Name is optional when a full name is entered in FirstName.
            if (!string.IsNullOrWhiteSpace(LastName) && LastName.Trim().Length < 2)
            {
                LastNameError = "Last name must be at least 2 characters";
                HasLastNameError = true;
                isValid = false;
            }

            // 3. Email validation
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

            // 4. Phone validation (optional, but if provided, must be valid)
            if (!string.IsNullOrWhiteSpace(Phone))
            {
                string cleanedPhone = Regex.Replace(Phone.Trim(), @"[\s\-\(\)\+]", "");
                if (cleanedPhone.Length < 7 || cleanedPhone.Length > 15 || !Regex.IsMatch(cleanedPhone, @"^\d+$"))
                {
                    PhoneError = "Please enter a valid phone number";
                    HasPhoneError = true;
                    isValid = false;
                }
            }

            // 5. Password validation
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

            // 6. Confirm Password validation
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

        private bool ValidateStep3()
        {
            bool isValid = true;

            // Clear previous errors
            CompanyNameError = string.Empty;
            HasCompanyNameError = false;

            BusinessTypeError = string.Empty;
            HasBusinessTypeError = false;

            AddressError = string.Empty;
            HasAddressError = false;

            CityError = string.Empty;
            HasCityError = false;

            StateError = string.Empty;
            HasStateError = false;

            // Company Name is the only required company field.
            if (string.IsNullOrWhiteSpace(CompanyName))
            {
                CompanyNameError = "Company name is required";
                HasCompanyNameError = true;
                isValid = false;
            }
            else if (CompanyName.Trim().Length < 2)
            {
                CompanyNameError = "Company name must be at least 2 characters";
                HasCompanyNameError = true;
                isValid = false;
            }

            return isValid;
        }

        // ══════════════════════════════════════════════════════════════
        // REGISTRATION SUBMISSION
        // ══════════════════════════════════════════════════════════════

        private async Task SubmitRegistrationAsync()
        {
            if (!NetworkStatus.HasInternet)
            {
                HasGeneralError = true;
                GeneralError = UserMessages.InternetRequired;
                return;
            }

            IsBusy = true;
            HasGeneralError = false;
            GeneralError = string.Empty;

            try
            {
                var (success, error) = await _auth.RegisterAsync(
                    FirstName.Trim(),
                    Email.Trim(),
                    Password,
                    Phone,
                    CompanyName.Trim());

                if (!success)
                {
                    HasGeneralError = true;
                    GeneralError = string.IsNullOrWhiteSpace(error) || error.Contains("Exception")
                        ? UserMessages.RegistrationFailed
                        : error;
                    return;
                }

                Password = string.Empty;
                ConfirmPassword = string.Empty;
                await Shell.Current.GoToAsync(AppRoutes.RegistrationSuccess);
            }
            catch
            {
                HasGeneralError = true;
                GeneralError = UserMessages.RegistrationFailed;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void EditUser() => CurrentStep = 2;

        [RelayCommand]
        private void EditCompany() => CurrentStep = 3;

        [RelayCommand]
        private void EditSubscription() => CurrentStep = 4;

        private void ApplyPlanConfiguration()
        {
            var plan = Plan.CreateFreeTrialCatalog();
            SelectedPlan = plan.Name;
            PlanDescription = plan.Description;
            PlanPrice = plan.Price;
            TrialDays = plan.TrialDays;
            CreditLimit = plan.CreditLimit;
            CreditsUsed = 0;
            RemainingCredits = CreditLimit - CreditsUsed;
            TrialStartDate = DateTime.UtcNow.Date;
            TrialEndDate = plan.CalculatePeriodEndDate(TrialStartDate);
        }

        // ══════════════════════════════════════════════════════════════
        // AUTO-CLEAR ERROR MESSAGES WHEN USER STARTS TYPING
        // ══════════════════════════════════════════════════════════════

        partial void OnFirstNameChanged(string value)
        {
            if (HasFirstNameError && !string.IsNullOrWhiteSpace(value))
            {
                HasFirstNameError = false;
                FirstNameError = string.Empty;
            }
        }

        partial void OnLastNameChanged(string value)
        {
            if (HasLastNameError && !string.IsNullOrWhiteSpace(value))
            {
                HasLastNameError = false;
                LastNameError = string.Empty;
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

        partial void OnPhoneChanged(string value)
        {
            if (HasPhoneError && !string.IsNullOrWhiteSpace(value))
            {
                HasPhoneError = false;
                PhoneError = string.Empty;
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
            if (HasConfirmPasswordError && !string.IsNullOrWhiteSpace(value))
            {
                HasConfirmPasswordError = false;
                ConfirmPasswordError = string.Empty;
            }
        }

        partial void OnCompanyNameChanged(string value)
        {
            if (HasCompanyNameError && !string.IsNullOrWhiteSpace(value))
            {
                HasCompanyNameError = false;
                CompanyNameError = string.Empty;
            }
        }

        partial void OnBusinessTypeChanged(string value)
        {
            if (HasBusinessTypeError && !string.IsNullOrWhiteSpace(value))
            {
                HasBusinessTypeError = false;
                BusinessTypeError = string.Empty;
            }
        }

        partial void OnAddressChanged(string value)
        {
            if (HasAddressError && !string.IsNullOrWhiteSpace(value))
            {
                HasAddressError = false;
                AddressError = string.Empty;
            }
        }

        partial void OnCityChanged(string value)
        {
            if (HasCityError && !string.IsNullOrWhiteSpace(value))
            {
                HasCityError = false;
                CityError = string.Empty;
            }
        }

        partial void OnStateChanged(string value)
        {
            if (HasStateError && !string.IsNullOrWhiteSpace(value))
            {
                HasStateError = false;
                StateError = string.Empty;
            }
        }
    }
}
