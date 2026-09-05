using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Navigation;

namespace AveroNova.App.UI.Pages.Returns;

public partial class PurchaseReturnFormPage : ContentPage, IHostedPage
{
    private readonly IReturnService _svc;
    private readonly ICompanyService _company;
    private readonly IPurchaseService _purchases;
    private readonly IMainContentNavigator _navigator;
    private List<PurchaseModel> _purchaseItems=[];
    private PurchaseReturnModel? _editing;
    public Guid? EditId{get;set;}

    public PurchaseReturnFormPage(IReturnService svc,ICompanyService company,IPurchaseService purchases,IMainContentNavigator navigator)
    {InitializeComponent();_svc=svc;_company=company;_purchases=purchases;_navigator=navigator;}

    protected override async void OnAppearing(){base.OnAppearing();await LoadForHostAsync();}
    public async Task LoadForHostAsync(){DateReturn.Date=DateTime.Today;StatusPicker.SelectedIndex=0;_purchaseItems=await _purchases.GetAllAsync(_company.CurrentCompany?.LocalId??Guid.Empty);PurchasePicker.ItemsSource=_purchaseItems.Select(x=>$"{x.PurchaseNumber} — {x.SupplierName}").ToList();if(EditId.HasValue&&_editing is null){_editing=await _svc.GetPurchaseReturnByIdAsync(EditId.Value);if(_editing is not null){PurchasePicker.SelectedIndex=_purchaseItems.FindIndex(x=>x.LocalId==_editing.PurchaseId);DateReturn.Date=_editing.ReturnDate;EntryRefund.Text=_editing.RefundAmount.ToString("0.##");ReasonPicker.SelectedItem=_editing.Reason;StatusPicker.SelectedIndex=(int)_editing.Status;EditorNotes.Text=_editing.Notes;}}}

    private async void OnSaveClicked(object s,EventArgs e)
    {
        if(PurchasePicker.SelectedIndex<0){await FailAsync("Select a purchase order.");return;}
        if(!decimal.TryParse(EntryRefund.Text,out var refund)||refund<=0){await FailAsync("Refund amount must be greater than zero.");return;}
        if(ReasonPicker.SelectedIndex<0){await FailAsync("Select a return reason.");return;}
        var purchase=_purchaseItems[PurchasePicker.SelectedIndex];
        var ret=_editing??new PurchaseReturnModel();
        ret.PurchaseId=purchase.LocalId;ret.PurchaseNumber=purchase.PurchaseNumber;ret.SupplierId=purchase.SupplierId;ret.SupplierName=purchase.SupplierName;ret.ReturnDate=DateReturn.Date??DateTime.Today;
        ret.Items=purchase.Items.Select(x=>new ReturnLineItem{ProductId=x.ProductId,ProductName=x.ProductName,Quantity=x.Quantity,UnitPrice=x.UnitPrice}).ToList();ret.RefundAmount=refund;ret.Reason=ReasonPicker.SelectedItem?.ToString()??"";ret.Notes=EditorNotes.Text?.Trim()??"";ret.Status=(ReturnStatus)Math.Max(0,StatusPicker.SelectedIndex);ret.CompanyId=_company.CurrentCompany?.LocalId??Guid.Empty;
        var isNew=_editing is null;
        var(ok,err)=isNew?await _svc.CreatePurchaseReturnAsync(ret):await _svc.UpdatePurchaseReturnAsync(ret);
        if(!ok){await FailAsync(err??"Unable to save purchase return.");return;}
        ErrorBanner.IsVisible=false;
        await AppToast.SuccessAsync(isNew?"Purchase return created successfully.":"Purchase return updated successfully.");
        await _navigator.GoBackAsync();
    }

    private async Task FailAsync(string message){LblError.Text=message;ErrorBanner.IsVisible=true;await AppToast.ErrorAsync(message);}
    private async void OnBackClicked(object s,EventArgs e)=>await _navigator.GoBackAsync();
}
