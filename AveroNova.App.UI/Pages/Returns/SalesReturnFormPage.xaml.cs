using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Returns;

public partial class SalesReturnFormPage : ContentPage
{
    private readonly IReturnService  _svc;
    private readonly ICompanyService _company;

    public SalesReturnFormPage(IReturnService svc, ICompanyService company)
    { InitializeComponent(); _svc = svc; _company = company; }

    protected override void OnAppearing() { base.OnAppearing(); DateReturn.Date = DateTime.Today; }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryInvoice.Text))  { ShowError("Invoice number is required."); return; }
        if (string.IsNullOrWhiteSpace(EntryCustomer.Text)) { ShowError("Customer name is required."); return; }
        if (!decimal.TryParse(EntryRefund.Text, out var refund)) { ShowError("Enter a valid refund amount."); return; }

        var ret = new SalesReturnModel
        {
            InvoiceNumber = EntryInvoice.Text.Trim(),
            CustomerName  = EntryCustomer.Text.Trim(),
            //ReturnDate    = DateReturn.Date,
            RefundAmount  = refund,
            Reason        = ReasonPicker.SelectedItem?.ToString() ?? "",
            Notes         = EditorNotes.Text?.Trim() ?? "",
            Status        = ReturnStatus.Pending,
            CompanyId     = _company.CurrentCompany?.LocalId ?? Guid.Empty
        };

        var (ok, err) = await _svc.CreateSalesReturnAsync(ret);
        if (ok) await Shell.Current.GoToAsync("..");
        else ShowError(err ?? "Save failed.");
    }

    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
