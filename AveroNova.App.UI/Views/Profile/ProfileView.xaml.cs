using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Views.Profile;

public partial class ProfileView : ContentView
{
    public ProfileView(ProfileViewModel viewModel)
    {
        InitializeComponent();

        BindingContext = viewModel;
    }
}