using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;


namespace AveroNova.App.UI.Pages.Returns;

public partial class PurchaseReturnFormPage : ContentPage
{
    private readonly IReturnService  _svc;
    private readonly ICompanyService _company;

    public PurchaseReturnFormPage(IReturnService svc, ICompanyService company)
    { InitializeComponent(); _svc = svc; _company = company; }

    protected override void OnAppearing() { base.OnAppearing(); DateReturn.Date = DateTime.Today; }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryPO.Text))       { ShowError("Purchase order number is required."); return; }
        if (string.IsNullOrWhiteSpace(EntrySupplier.Text)) { ShowError("Supplier name is required."); return; }
        if (!decimal.TryParse(EntryRefund.Text, out var refund)) { ShowError("Enter a valid refund amount."); return; }

        var ret = new PurchaseReturnModel
        {
            PurchaseNumber = EntryPO.Text.Trim(),
            SupplierName   = EntrySupplier.Text.Trim(),
            //ReturnDate     = DateReturn.Date,
            RefundAmount   = refund,
            Reason         = ReasonPicker.SelectedItem?.ToString() ?? "",
            Notes          = EditorNotes.Text?.Trim() ?? "",
            Status         = ReturnStatus.Pending,
            CompanyId      = _company.CurrentCompany?.LocalId ?? Guid.Empty
        };

        var (ok, err) = await _svc.CreatePurchaseReturnAsync(ret);
        if (ok) await Shell.Current.GoToAsync("..");
        else ShowError(err ?? "Save failed.");
    }

    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
