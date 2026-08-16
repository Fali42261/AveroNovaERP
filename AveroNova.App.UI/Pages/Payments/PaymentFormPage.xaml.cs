using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Payments;

// OFFLINE: Cash/payment records may be created locally and synchronized when connectivity is restored.
// TODO: Implement PaymentService + local persistence + SyncQueue during backend phase.

[QueryProperty(nameof(EditId), "id")]
public partial class PaymentFormPage : ContentPage
{
    private readonly IPaymentService _svc;
    private readonly ICompanyService _company;
    private PaymentModel? _editing;
    public string? EditId { get; set; }

    public PaymentFormPage(IPaymentService svc, ICompanyService company)
    { InitializeComponent(); _svc = svc; _company = company; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        DatePayment.Date = DateTime.Today;
        if (!string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var id))
        {
            _editing = await _svc.GetByIdAsync(id);
            if (_editing != null)
            {
                EntryParty.Text      = _editing.PartyName;
                EntryInvoice.Text    = _editing.InvoiceNumber;
                EntryAmount.Text     = _editing.Amount.ToString("N2");
                DatePayment.Date     = _editing.PaymentDate;
                EntryRef.Text        = _editing.Reference;
                EditorNotes.Text     = _editing.Notes;
            }
        }
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryParty.Text))  { ShowError("Party name is required."); return; }
        if (!decimal.TryParse(EntryAmount.Text, out var amt) || amt <= 0) { ShowError("Enter a valid amount."); return; }
        if (MethodPicker.SelectedIndex < 0) { ShowError("Select a payment method."); return; }

        var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        var m   = _editing ?? new PaymentModel { CompanyId = cid };
        m.PartyName      = EntryParty.Text.Trim();
        m.InvoiceNumber  = EntryInvoice.Text?.Trim() ?? "";
        m.Amount         = amt;
        //m.PaymentDate    = DatePayment.Date.ToString("dd/MM/yyyy");
        m.Reference      = EntryRef.Text?.Trim() ?? "";
        m.Notes          = EditorNotes.Text?.Trim() ?? "";
        m.Status         = PaymentStatus.Completed;
        var methods      = new[] { PaymentMethod.Cash, PaymentMethod.BankTransfer, PaymentMethod.CreditCard, PaymentMethod.DebitCard, PaymentMethod.Cheque, PaymentMethod.Online };
        m.Method         = MethodPicker.SelectedIndex >= 0 ? methods[MethodPicker.SelectedIndex] : PaymentMethod.Cash;
        if (string.IsNullOrEmpty(m.PaymentNumber)) m.PaymentNumber = await _svc.GetNextPaymentNumberAsync(cid);

        var (ok, err) = _editing == null ? await _svc.CreateAsync(m) : await _svc.UpdateAsync(m);
        if (ok) await Shell.Current.GoToAsync("..");
        else ShowError(err ?? "Save failed.");
    }

    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
