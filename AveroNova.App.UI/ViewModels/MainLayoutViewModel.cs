using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

namespace AveroNova.App.UI.ViewModels
{
    public class MainLayoutViewModel : INotifyPropertyChanged
    {
        private string _currentPage = "Dashboard";

        public string CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage == value)
                    return;

                _currentPage = value;

                OnPropertyChanged();
            }
        }

        public ICommand DashboardCommand { get; }

        public ICommand ProfileCommand { get; }

        public ICommand CompanyCommand { get; }


        public MainLayoutViewModel()
        {
            DashboardCommand = new Command(
                () => CurrentPage = "Dashboard");

            ProfileCommand = new Command(
                () => CurrentPage = "Profile");

            CompanyCommand = new Command(
                () => CurrentPage = "Company Details");
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}
