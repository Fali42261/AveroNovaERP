using AveroNova.App.UI.Views.Layout;

namespace AveroNova.App.UI;

public partial class MainPage : ContentPage
{
    public MainPage(MainLayoutView layout)
    {
        InitializeComponent();
        LayoutHost.Content = layout;
    }
}
