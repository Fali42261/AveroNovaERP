using System.ComponentModel;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Views.Company;

public partial class CompanySidebarView : ContentView
{
	public CompanySidebarView()
	{
		InitializeComponent();
        BindingContextChanged += CompanySidebarView_BindingContextChanged;
    }
    private void CompanySidebarView_BindingContextChanged(object? sender, EventArgs e)
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
            if (sender is CompanySetupViewModel vm)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateStep(vm.CurrentStep);
                });
            }
        }
    }

    private void UpdateStep(int step)
    {
        CompanyItem.IsActive = (step == 1);
        //TeamItem.IsActive = (step == 2);
        //AdminItem.IsActive = (step == 3);
        FinishItem.IsActive = (step == 2);
    }
}