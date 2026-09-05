using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Navigation;

namespace AveroNova.App.UI.Pages.Returns;

public partial class SalesReturnFormPage : ContentPage, IHostedPage
{
    private readonly IReturnService  _svc;
    private readonly ICompanyService _company;
    private readonly IBillingService _billing;private readonly IMainContentNavigator _navigator;private List<InvoiceModel> _invoices=[];
    private SalesReturnModel? _editing;public Guid? EditId{get;set;}

    public SalesReturnFormPage(IReturnService svc, ICompanyService company, IBillingService billing, IMainContentNavigator navigator)
    { InitializeComponent(); _svc = svc; _company = company; _billing=billing; _navigator=navigator; }

    protected override async void OnAppearing() { base.OnAppearing(); await LoadForHostAsync(); }
    public async Task LoadForHostAsync(){DateReturn.Date=DateTime.Today;StatusPicker.SelectedIndex=0;_invoices=await _billing.GetAllAsync(_company.CurrentCompany?.LocalId??Guid.Empty);InvoicePicker.ItemsSource=_invoices.Select(x=>$"{x.InvoiceNumber} — {x.CustomerName}").ToList();if(EditId.HasValue&&_editing is null){_editing=await _svc.GetSalesReturnByIdAsync(EditId.Value);if(_editing is not null){InvoicePicker.SelectedIndex=_invoices.FindIndex(x=>x.LocalId==_editing.InvoiceId);DateReturn.Date=_editing.ReturnDate;EntryRefund.Text=_editing.RefundAmount.ToString("0.##");ReasonPicker.SelectedItem=_editing.Reason;StatusPicker.SelectedIndex=(int)_editing.Status;EditorNotes.Text=_editing.Notes;}}}

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (InvoicePicker.SelectedIndex<0)  { ShowError("Select an invoice."); return; }
        if (!decimal.TryParse(EntryRefund.Text, out var refund)||refund<=0) { ShowError("Refund amount must be greater than zero."); return; }
        if(ReasonPicker.SelectedIndex<0){ShowError("Select a return reason.");return;}var invoice=_invoices[InvoicePicker.SelectedIndex];

        var ret = _editing??new SalesReturnModel();
        ret.InvoiceId=invoice.LocalId;ret.InvoiceNumber=invoice.InvoiceNumber;ret.CustomerId=invoice.CustomerId;ret.CustomerName=invoice.CustomerName;
        ret.ReturnDate=DateReturn.Date??DateTime.Today;ret.Items=invoice.Items.Select(x=>new ReturnLineItem{ProductId=x.ProductId,ProductName=x.ProductName,Quantity=x.Quantity,UnitPrice=x.UnitPrice}).ToList();
        ret.RefundAmount=refund;ret.Reason=ReasonPicker.SelectedItem?.ToString()??"";ret.Notes=EditorNotes.Text?.Trim()??"";ret.Status=(ReturnStatus)Math.Max(0,StatusPicker.SelectedIndex);ret.CompanyId=_company.CurrentCompany?.LocalId??Guid.Empty;
        var (ok, err) = _editing is null?await _svc.CreateSalesReturnAsync(ret):await _svc.UpdateSalesReturnAsync(ret);
        if (ok) await _navigator.GoBackAsync();
        else ShowError(err ?? "Save failed.");
    }

    private async void OnBackClicked(object s, EventArgs e) => await _navigator.GoBackAsync();
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
