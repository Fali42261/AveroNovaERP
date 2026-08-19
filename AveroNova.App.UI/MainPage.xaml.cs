using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Views.Layout;

namespace AveroNova.App.UI;

public partial class MainPage : ContentPage
{
    private readonly IToastService _toasts;

    public MainPage(MainLayoutView layout, IToastService toasts)
    {
        AveroNova.App.UI.Helpers.StartupLog.Write("MainPage ctor start");
        InitializeComponent();
        AveroNova.App.UI.Helpers.StartupLog.Write("MainPage InitializeComponent done");
        _toasts = toasts;
        LayoutHost.Content = layout;
        AveroNova.App.UI.Helpers.StartupLog.Write("MainPage layout assigned");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _toasts.AttachTo(this);
    }
}
