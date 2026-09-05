using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Administration;

using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

public partial class PermissionsPage : ContentPage, IHostedPage
{
    private readonly IUserService _service;private List<AveroNova.App.UI.Models.PermissionModel> _all=[];
    public PermissionsPage(IUserService service)
    {
        InitializeComponent();
        _service=service;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }

    public async Task LoadForHostAsync(){_all=await _service.GetPermissionsAsync();LoadContent();}
    private async void OnRefreshing(object s, EventArgs e) { await LoadForHostAsync(); Refresher.IsRefreshing = false; }
    private async void OnRefreshClicked(object s, EventArgs e) { await LoadForHostAsync(); }

    private void OnSearchChanged(object s, TextChangedEventArgs e)
    {
        LoadContent();
    }

    private void LoadContent()
    {
        PermissionsContent.Children.Clear();
        var q=SearchBar.Text?.Trim()??"";var shown=string.IsNullOrEmpty(q)?_all:_all.Where(x=>x.Key.Contains(q,StringComparison.OrdinalIgnoreCase)||x.Module.Contains(q,StringComparison.OrdinalIgnoreCase)||x.Label.Contains(q,StringComparison.OrdinalIgnoreCase)).ToList();foreach(var group in shown.GroupBy(x=>x.Module)){PermissionsContent.Children.Add(new Label{Text=group.Key,FontSize=16,FontAttributes=FontAttributes.Bold});foreach(var p in group)PermissionsContent.Children.Add(new Label{Text=$"{p.Label}\n{p.Key}",FontSize=13,TextColor=Color.FromArgb("#475569"),Margin=new Thickness(8,2)});}if(shown.Count==0)PermissionsContent.Children.Add(new Label{Text="No permissions found.",HorizontalOptions=LayoutOptions.Center,Margin=new Thickness(0,40)});
    }
}
