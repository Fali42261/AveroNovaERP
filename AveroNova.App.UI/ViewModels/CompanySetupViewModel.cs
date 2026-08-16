using AveroNova.Application.Interfaces;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AveroNova.App.UI.ViewModels
{
    public class CompanySetupViewModel : INotifyPropertyChanged
    {
        private readonly ICompanyService _companyService;


        public ICommand NextCommand { get; }
        public ICommand BackCommand { get; }
        public CompanySetupViewModel(ICompanyService companyService)
        {
            _companyService = companyService;

            NextCommand = new Command(OnNext);
            BackCommand = new Command(OnBack);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _companyName = string.Empty;

        public string CompanyName
        {
            get => _companyName;
            set
            {
                _companyName = value;
                OnPropertyChanged();
            }
        }

        private string _ownerName = string.Empty;
        public string OwnerName
        {
            get => _ownerName;
            set
            {
                _ownerName = value;
                OnPropertyChanged();
            }
        }

        private string _gstNumber = string.Empty;
        public string GSTNumber
        {
            get => _gstNumber;
            set
            {
                _gstNumber = value;
                OnPropertyChanged();
            }
        }

        private string _email = string.Empty;
        public string Email
        {
            get => _email;
            set
            {
                _email = value;
                OnPropertyChanged();
            }
        }

        private string _mobileNumber = string.Empty;
        public string MobileNumber
        {
            get => _mobileNumber;
            set
            {
                _mobileNumber = value;
                OnPropertyChanged();
            }
        }

        private string _country = string.Empty;
        public string Country
        {
            get => _country;
            set
            {
                _country = value;
                OnPropertyChanged();
            }
        }

        private string _city = string.Empty;
        public string City
        {
            get => _city;
            set
            {
                _city = value;
                OnPropertyChanged();
            }
        }

        private string _pinCode = string.Empty;
        public string PinCode
        {
            get => _pinCode;
            set
            {
                _pinCode = value;
                OnPropertyChanged();
            }
        }

        private string _panNumber = string.Empty;
        public string PanNumber
        {
            get => _panNumber;
            set
            {
                _panNumber = value;
                OnPropertyChanged();
            }
        }

        private string _address = string.Empty;
        public string Address
        {
            get => _address;
            set
            {
                _address = value;
                OnPropertyChanged();
            }
        }

        private string _state = string.Empty;
        public string State
        {
            get => _state;
            set
            {
                _state = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> BusinessTypes { get; } =
[
    "Select Business Type",
    "Retail",
    "Wholesale",
    "Manufacturing",
    "Service"
];


        private string _selectedBusinessType = "Select Business Type";

        public string SelectedBusinessType
        {
            get => _selectedBusinessType;
            set
            {
                _selectedBusinessType = value;
                OnPropertyChanged();
            }
        }


        public ObservableCollection<string> Industries { get; } =
[
    "Select Industry",
    "IT",
    "Healthcare",
    "Education",
    "Manufacturing"
];

        private string _selectedIndustry = "Select Industry";

        public string SelectedIndustry
        {
            get => _selectedIndustry;
            set
            {
                _selectedIndustry = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> Currencies { get; } =
[
    "Select Currency",
    "INR",
    "USD",
    "AED"
];

        private string _selectedCurrency = "Select Currency";

        public string SelectedCurrency
        {
            get => _selectedCurrency;
            set
            {
                _selectedCurrency = value;
                OnPropertyChanged();
            }
        }


        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(CompanyName))
                return false;

            if (string.IsNullOrWhiteSpace(OwnerName))
                return false;

            if (string.IsNullOrWhiteSpace(GSTNumber))
                return false;

            if (string.IsNullOrWhiteSpace(Email))
                return false;

            if (string.IsNullOrWhiteSpace(MobileNumber))
                return false;

            if (string.IsNullOrWhiteSpace(Country))
                return false;

            if (string.IsNullOrWhiteSpace(City))
                return false;

            if (string.IsNullOrWhiteSpace(PinCode))
                return false;

            if (string.IsNullOrWhiteSpace(PanNumber))
                return false;

            if (string.IsNullOrWhiteSpace(Address))
                return false;

            if (string.IsNullOrWhiteSpace(State))
                return false;

            //if (SelectedBusinessType == "Select Business Type")
            //    return false;

            //if (SelectedIndustry == "Select Industry")
            //    return false;

            return true;
        }

        private int _currentStep = 1;
        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                if (_currentStep != value)
                {
                    _currentStep = value;

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CurrentTitle));
                    OnPropertyChanged(nameof(CurrentDescription));
                }
            }
        }

        public void OnNext()
        {
            if (CurrentStep < 2)
            {
                CurrentStep++;
            }

            System.Diagnostics.Debug.WriteLine($"Current Step : {CurrentStep}");
        }

        public void OnBack()
        {
            if (CurrentStep > 1)
            {
                CurrentStep--;
            }
            System.Diagnostics.Debug.WriteLine($"Current Step : {CurrentStep}");
        }

        public string CurrentTitle
        {
            get
            {
                return CurrentStep switch
                {
                    1 => "Company Information",
                    //2 => "Team Setup",
                    //3 => "Admin Account",
                    2 => "Review & Finish",
                    _ => string.Empty
                };
            }
        }

        public string CurrentDescription
        {
            get
            {
                return CurrentStep switch
                {
                    1 => "Tell us about your organization.",
                    //2 => "Configure your first team.",
                    //3 => "Create administrator account.",
                    2 => "Review all information before creating your company.",
                    _ => string.Empty
                };
            }
        }




        //public async Task SaveCompanyAsync()
        //{

        //}
    }
}
