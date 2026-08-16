using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AveroNova.App.UI.Pages.Authentication;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial string FirstName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string LastName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Email { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Mobile { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Password { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ConfirmPassword { get; set; } = string.Empty;

        // Password visibility toggles
        [ObservableProperty]
        public partial bool IsPasswordHidden { get; set; } = true;

        [ObservableProperty]
        public partial bool IsConfirmPasswordHidden { get; set; } = true;

        // Toggle button labels
        [ObservableProperty]
        public partial string ShowPasswordText { get; set; } = "Show";

        [ObservableProperty]
        public partial string ShowConfirmPasswordText { get; set; } = "Show";

        // Error message properties
        [ObservableProperty]
        public partial string FirstNameError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string LastNameError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string EmailError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string MobileError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string PasswordError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ConfirmPasswordError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string GeneralError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string GeneralSuccess { get; set; } = string.Empty;

        // Has error flags for easy XAML visibility bindings
        [ObservableProperty]
        public partial bool HasFirstNameError { get; set; }

        [ObservableProperty]
        public partial bool HasLastNameError { get; set; }

        [ObservableProperty]
        public partial bool HasEmailError { get; set; }

        [ObservableProperty]
        public partial bool HasMobileError { get; set; }

        [ObservableProperty]
        public partial bool HasPasswordError { get; set; }

        [ObservableProperty]
        public partial bool HasConfirmPasswordError { get; set; }

        [ObservableProperty]
        public partial bool HasGeneralError { get; set; }

        [ObservableProperty]
        public partial bool HasGeneralSuccess { get; set; }

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        public partial string BusyText { get; set; } = "Creating account...";

        public RegisterViewModel()
        {
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
        private async Task RegisterAsync()
        {
            if (!ValidateForm())
            {
                return;
            }

            IsBusy = true;
            HasGeneralError = false;
            GeneralError = string.Empty;
            HasGeneralSuccess = false;
            GeneralSuccess = string.Empty;

            try
            {
                // UI-level simulation - no backend/API calls
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
                // Fallback for non-shell navigation contexts
                if (Microsoft.Maui.Controls.Application.Current?.Windows.Count > 0 && 
                    Microsoft.Maui.Controls.Application.Current.Windows[0].Page?.Navigation != null)
                {
                    //await Shell.Current.GoToAsync(nameof(LoginPage));
                    await Shell.Current.GoToAsync("//LoginPage");
                    //await Microsoft.Maui.Controls.Application.Current
                    //    .Windows[0].Page!.Navigation.PushAsync(new LoginPage());
                }
            }
        }

        public bool ValidateForm()
        {
            bool isValid = true;

            // Clear previous errors
            FirstNameError = string.Empty;
            HasFirstNameError = false;

            LastNameError = string.Empty;
            HasLastNameError = false;

            EmailError = string.Empty;
            HasEmailError = false;

            MobileError = string.Empty;
            HasMobileError = false;

            PasswordError = string.Empty;
            HasPasswordError = false;

            ConfirmPasswordError = string.Empty;
            HasConfirmPasswordError = false;

            HasGeneralError = false;
            GeneralError = string.Empty;
            HasGeneralSuccess = false;
            GeneralSuccess = string.Empty;

            // 1. First Name validation
            if (string.IsNullOrWhiteSpace(FirstName))
            {
                FirstNameError = "First name is required";
                HasFirstNameError = true;
                isValid = false;
            }
            else if (FirstName.Trim().Length < 2)
            {
                FirstNameError = "First name must be at least 2 characters";
                HasFirstNameError = true;
                isValid = false;
            }

            // 2. Last Name validation
            if (string.IsNullOrWhiteSpace(LastName))
            {
                LastNameError = "Last name is required";
                HasLastNameError = true;
                isValid = false;
            }
            else if (LastName.Trim().Length < 2)
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

            // 4. Mobile validation
            if (string.IsNullOrWhiteSpace(Mobile))
            {
                MobileError = "Mobile number is required";
                HasMobileError = true;
                isValid = false;
            }
            else
            {
                string cleanedMobile = Regex.Replace(Mobile.Trim(), @"[\s\-\(\)\+]", "");
                if (cleanedMobile.Length < 7 || cleanedMobile.Length > 15 || !Regex.IsMatch(cleanedMobile, @"^\d+$"))
                {
                    MobileError = "Please enter a valid mobile number";
                    HasMobileError = true;
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

        partial void OnMobileChanged(string value)
        {
            if (HasMobileError && !string.IsNullOrWhiteSpace(value))
            {
                HasMobileError = false;
                MobileError = string.Empty;
            }
        }

        partial void OnPasswordChanged(string value)
        {
            if (HasPasswordError && !string.IsNullOrWhiteSpace(value))
            {
                HasPasswordError = false;
                PasswordError = string.Empty;
            }

            if (HasConfirmPasswordError && !string.IsNullOrWhiteSpace(ConfirmPassword) && value == ConfirmPassword)
            {
                HasConfirmPasswordError = false;
                ConfirmPasswordError = string.Empty;
            }
        }

        partial void OnConfirmPasswordChanged(string value)
        {
            if (HasConfirmPasswordError && (!string.IsNullOrWhiteSpace(value) && value == Password))
            {
                HasConfirmPasswordError = false;
                ConfirmPasswordError = string.Empty;
            }
        }
    }
}
