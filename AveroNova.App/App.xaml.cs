using AveroNova.App.Pages;

namespace AveroNova.App
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        private readonly CompanyPage _companyPage;

        public App(CompanyPage companyPage)
        {
            InitializeComponent();

            _companyPage = companyPage;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage()) { Title = "AveroNova.App" };
        }
    }
}
