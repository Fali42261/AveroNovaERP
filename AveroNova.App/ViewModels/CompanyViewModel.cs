using AveroNova.Application.Interfaces;
using AveroNova.Domain.Entities;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AveroNova.App.ViewModels
{
    public class CompanyViewModel : INotifyPropertyChanged
    {
        private readonly ICompanyService _companyService;

        public CompanyViewModel(ICompanyService companyService)
        {
            _companyService = companyService;
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

        //[RelayCommand]
        public async Task SaveCompanyAsync()
        {
            var company = new Company
            {
                CompanyCode = "CMP001",
                CompanyName = CompanyName,
                OwnerName = "Test Owner",
                Email = "test@test.com",
                MobileNumber = "9999999999",
                GSTNumber = "GST123",
                Address = "Test Address",
                State = "UP",
                City = "Noida",
                PinCode = "201301"
            };

            await _companyService.AddAsync(company);

            CompanyName = string.Empty;
        }
    }
}
