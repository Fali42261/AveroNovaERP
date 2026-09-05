using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Purchases;

public partial class SuppliersListPage : ContentPage, IHostedPage
{
    private readonly ISupplierService _service; private readonly ICompanyService _company; private readonly IMainContentNavigator _nav; private readonly Func<SupplierFormPage> _formFactory;
    public SuppliersListPage(ISupplierService service,ICompanyService company,IMainContentNavigator nav,Func<SupplierFormPage> formFactory){InitializeComponent();_service=service;_company=company;_nav=nav;_formFactory=formFactory;}
    protected override async void OnAppearing(){base.OnAppearing();await LoadForHostAsync();}
    public async Task LoadForHostAsync(){var rows=await _service.GetAllAsync(_company.CurrentCompany?.LocalId??Guid.Empty);LblCount.Text=$"{rows.Count} supplier{(rows.Count==1?"":"s")}";List.Children.Clear();foreach(var x in rows)List.Children.Add(Row(x));if(rows.Count==0)List.Children.Add(new Label{Text="No suppliers found. Create a supplier before adding a purchase.",HorizontalOptions=LayoutOptions.Center,Margin=new Thickness(0,40),TextColor=Color.FromArgb("#64748B")});}
    private View Row(SupplierModel x){var border=new Border{Style=(Style)Resources["AppCard"]};var grid=new Grid{ColumnDefinitions=new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)),ColumnSpacing=10};var info=new VerticalStackLayout{Spacing=3};info.Children.Add(new Label{Text=x.Name,FontAttributes=FontAttributes.Bold,FontSize=15});info.Children.Add(new Label{Text=string.Join(" • ",new[]{x.Phone,x.Email}.Where(s=>!string.IsNullOrWhiteSpace(s))),FontSize=12,TextColor=Color.FromArgb("#64748B")});var edit=new Button{Text="Edit",Style=(Style)Resources["SmallSecondaryButton"]};edit.Clicked+=async(_,_)=>{var page=_formFactory();page.EditId=x.LocalId;await _nav.NavigateAsync(page,"Edit Supplier","Home / Purchases / Suppliers / Edit");};grid.Add(info);grid.Add(edit,1,0);border.Content=grid;return border;}
    private async void OnRefreshing(object s,EventArgs e){await LoadForHostAsync();Refresher.IsRefreshing=false;}
    private async void OnNewClicked(object s,EventArgs e)=>await _nav.NavigateAsync(_formFactory(),"New Supplier","Home / Purchases / Suppliers / New");
    private async void OnBackClicked(object s,EventArgs e)=>await _nav.GoBackAsync();
}
