using AveroNova.App.UI.ViewModels;
using System.ComponentModel;

namespace AveroNova.App.UI.Views.Shared;

public partial class SidebarView : ContentView
{
	public SidebarView()
	{
		InitializeComponent();
        BindingContextChanged += SidebarView_BindingContextChanged;
    }
    private void SidebarView_BindingContextChanged(object? sender, EventArgs e)
    {
        if (BindingContext is CompanySetupViewModel vm)
        {
            vm.PropertyChanged -= Vm_PropertyChanged;
            vm.PropertyChanged += Vm_PropertyChanged;

            UpdateStep(vm.CurrentStep);
        }
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CompanySetupViewModel.CurrentStep))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (sender is CompanySetupViewModel vm)
                {
                    UpdateStep(vm.CurrentStep);
                }
            });
        }
    }

    private void UpdateStep(int step)
    {
        // Reset all
        CompanyBorder.BackgroundColor = Colors.Transparent;
        TeamBorder.BackgroundColor = Colors.Transparent;
        AdminBorder.BackgroundColor = Colors.Transparent;
        FinishBorder.BackgroundColor = Colors.Transparent;

        switch (step)
        {
            case 1:
                CompanyBorder.BackgroundColor = Color.FromArgb("#2563EB");
                break;

            case 2:
                TeamBorder.BackgroundColor = Color.FromArgb("#2563EB");
                break;

            case 3:
                AdminBorder.BackgroundColor = Color.FromArgb("#2563EB");
                break;

            case 4:
                FinishBorder.BackgroundColor = Color.FromArgb("#2563EB");
                break;
        }
    }
}