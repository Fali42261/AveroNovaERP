namespace AveroNova.App.UI.Pages.Reports;

public partial class ReportsPage : ContentPage
{
    public ReportsPage()
    {
        InitializeComponent();
    }

    private void OnRefreshing(object s, EventArgs e)
    {
        Refresher.IsRefreshing = false;
    }
}
