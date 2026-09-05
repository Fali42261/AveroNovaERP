using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Payments;

[QueryProperty(nameof(EditId), "id")]
public partial class PaymentFormPage : ContentPage
{
    private readonly IPaymentService _svc;
    private readonly ICompanyService _company;
    private readonly IBillingService _billing;
    private List<InvoiceModel> _invoices = [];
    private PaymentModel? _editing;
    public string? EditId { get; set; }

    public PaymentFormPage(IPaymentService svc, ICompanyService company, IBillingService billing)
    { InitializeComponent(); _svc = svc; _company = company; _billing = billing; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        DatePayment.Date = DateTime.Today;
        var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        _invoices = (await _billing.GetAllAsync(cid))
            .Where(x => x.Status is not InvoiceStatus.Cancelled and not InvoiceStatus.Paid && x.DueAmount > 0)
            .OrderByDescending(x => x.InvoiceDate).ToList();
        InvoicePicker.ItemsSource = _invoices.Select(x => $"{x.InvoiceNumber} — {x.CustomerName} — Due {x.DueAmount:N2}").ToList();

        if (!string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var id))
        {
            _editing = await _svc.GetByIdAsync(id);
            if (_editing != null)
            {
                var idx = _invoices.FindIndex(x => x.LocalId == _editing.InvoiceId);
                if (idx >= 0) InvoicePicker.SelectedIndex = idx;
                EntryParty.Text = _editing.PartyName;
                EntryAmount.Text = _editing.Amount.ToString("N2");
                DatePayment.Date = _editing.PaymentDate;
                EntryRef.Text = _editing.Reference;
                EditorNotes.Text = _editing.Notes;
                MethodPicker.SelectedIndex = _editing.Method == PaymentMethod.Online ? 1 : 0;
            }
        }
    }

    private void OnInvoiceSelected(object sender, EventArgs e)
    {
        if (InvoicePicker.SelectedIndex < 0 || InvoicePicker.SelectedIndex >= _invoices.Count) return;
        var inv = _invoices[InvoicePicker.SelectedIndex];
        EntryParty.Text = inv.CustomerName;
        LblDue.Text = $"Due: {inv.DueAmount:N2}";
        if (_editing is null) EntryAmount.Text = inv.DueAmount.ToString("N2");
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (InvoicePicker.SelectedIndex < 0) { await FailAsync("Select an invoice."); return; }
        if (!decimal.TryParse(EntryAmount.Text, out var amt) || amt <= 0) { await FailAsync("Enter a valid amount."); return; }
        if (MethodPicker.SelectedIndex < 0) { await FailAsync("Select Cash or Online payment type."); return; }

        var inv = _invoices[InvoicePicker.SelectedIndex];
        if (amt > inv.DueAmount + (_editing?.Amount ?? 0m) + 0.01m) { await FailAsync("Payment amount cannot exceed invoice due amount."); return; }

        var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        var isNew = _editing is null;
        var m = _editing ?? new PaymentModel { CompanyId = cid };
        m.PartyId = inv.CustomerId;
        m.PartyName = inv.CustomerName;
        m.IsSupplier = false;
        m.InvoiceId = inv.LocalId;
        m.InvoiceNumber = inv.InvoiceNumber;
        m.Amount = amt;
        m.PaymentDate = DatePayment.Date;
        m.Reference = EntryRef.Text?.Trim() ?? "";
        m.Notes = EditorNotes.Text?.Trim() ?? "";
        m.Status = PaymentStatus.Completed;
        m.Method = MethodPicker.SelectedIndex == 1 ? PaymentMethod.Online : PaymentMethod.Cash;
        if (string.IsNullOrEmpty(m.PaymentNumber)) m.PaymentNumber = await _svc.GetNextPaymentNumberAsync(cid);

        var (ok, err) = isNew ? await _svc.CreateAsync(m) : await _svc.UpdateAsync(m);
        if (!ok) { await FailAsync(err ?? "Unable to save payment."); return; }

        await AppToast.SuccessAsync(isNew ? "Payment recorded successfully." : "Payment updated successfully.");
        await Shell.Current.GoToAsync("..");
    }

    private async Task FailAsync(string message)
    {
        ShowError(message);
        await AppToast.ErrorAsync(message);
    }

    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
