namespace AveroNova.App.UI.Layouts;

public partial class MainLayout : ContentView
{
	public MainLayout()
	{
		InitializeComponent();
	}


    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>();

#if WINDOWS
    Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("Borderless", (handler, view) =>
    {
        handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
    });
#endif

        return builder.Build();
    }

}