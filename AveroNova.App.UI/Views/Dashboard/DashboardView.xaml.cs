using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Views.Dashboard;

public partial class DashboardView : ContentView
{
    public DashboardView(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
