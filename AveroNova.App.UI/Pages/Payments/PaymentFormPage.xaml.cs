using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Payments;

[QueryProperty(nameof(EditId), "id")]
public partial class PaymentFormPage : ContentPage
{
    private readonly IPaymentService _svc;
    private readonly ICompanyService _company;
    private PaymentModel? _editing;
    public string? EditId { get; set; }
    public Action? CloseRequested { get; set; }

    public PaymentFormPage(IPaymentService svc, ICompanyService company)
    { InitializeComponent(); _svc = svc; _company = company; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Guid? id = !string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var parsed) ? parsed : null;
        await LoadAsync(id);
    }

    public async Task LoadAsync(Guid? id = null)
    {
        ErrorBanner.IsVisible = false; _editing = null;
        EntryParty.Text = string.Empty; EntryInvoice.Text = string.Empty; EntryAmount.Text = string.Empty;
        EntryRef.Text = string.Empty; EditorNotes.Text = string.Empty; MethodPicker.SelectedIndex = -1; DatePayment.Date = DateTime.Today;
        if (id.HasValue && id.Value != Guid.Empty)
        {
            _editing = await _svc.GetByIdAsync(id.Value);
            if (_editing != null)
            {
                EntryParty.Text = _editing.PartyName; EntryInvoice.Text = _editing.InvoiceNumber; EntryAmount.Text = _editing.Amount.ToString("0.00");
                DatePayment.Date = _editing.PaymentDate; EntryRef.Text = _editing.Reference; EditorNotes.Text = _editing.Notes;
                var methods = new[] { PaymentMethod.Cash, PaymentMethod.BankTransfer, PaymentMethod.CreditCard, PaymentMethod.DebitCard, PaymentMethod.Cheque, PaymentMethod.Online };
                MethodPicker.SelectedIndex = Array.IndexOf(methods, _editing.Method);
            }
        }
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        ErrorBanner.IsVisible = false;
        if (string.IsNullOrWhiteSpace(EntryParty.Text)) { ShowError("Party name is required."); return; }
        if (!decimal.TryParse(EntryAmount.Text, out var amt) || amt <= 0) { ShowError("Enter a valid amount."); return; }
        if (MethodPicker.SelectedIndex < 0) { ShowError("Select a payment method."); return; }
        var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        if (cid == Guid.Empty) { ShowError("Company is required."); return; }
        var m = _editing ?? new PaymentModel { CompanyId = cid };
        m.CompanyId = cid; m.PartyName = EntryParty.Text.Trim(); m.InvoiceNumber = EntryInvoice.Text?.Trim() ?? ""; m.Amount = amt;
        m.PaymentDate = DatePayment.Date ?? DateTime.Today; m.Reference = EntryRef.Text?.Trim() ?? ""; m.Notes = EditorNotes.Text?.Trim() ?? ""; m.Status = PaymentStatus.Completed;
        var methods = new[] { PaymentMethod.Cash, PaymentMethod.BankTransfer, PaymentMethod.CreditCard, PaymentMethod.DebitCard, PaymentMethod.Cheque, PaymentMethod.Online };
        m.Method = methods[MethodPicker.SelectedIndex];
        if (string.IsNullOrEmpty(m.PaymentNumber)) m.PaymentNumber = await _svc.GetNextPaymentNumberAsync(cid);
        var (ok, err) = _editing == null ? await _svc.CreateAsync(m) : await _svc.UpdateAsync(m);
        if (ok) await CloseAsync(); else ShowError(err ?? "Save failed.");
    }

    private async Task CloseAsync()
    {
        if (CloseRequested != null) { CloseRequested.Invoke(); return; }
        await Shell.Current.GoToAsync("..");
    }

    private async void OnBackClicked(object s, EventArgs e) => await CloseAsync();
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
