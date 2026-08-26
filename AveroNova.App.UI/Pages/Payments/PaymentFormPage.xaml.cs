using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Payments;

[QueryProperty(nameof(EditId), "id")]
public partial class PaymentFormPage : ContentPage
{
    private readonly IPaymentService _svc;
    private readonly ICompanyService _company;
    private readonly IBillingService _billing;
    private readonly IPurchaseService _purchases;
    private readonly List<InvoiceModel> _invoices = [];
    private readonly List<PurchaseModel> _purchaseDocs = [];
    private PaymentModel? _editing;
    public string? EditId { get; set; }
    public Action? CloseRequested { get; set; }

    public PaymentFormPage(IPaymentService svc, ICompanyService company, IBillingService billing, IPurchaseService purchases)
    { InitializeComponent(); _svc = svc; _company = company; _billing = billing; _purchases = purchases; }

    protected override async void OnAppearing()
    {
        base.OnAppearing(); Guid? id = !string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var parsed) ? parsed : null; await LoadAsync(id);
    }

    public async Task LoadAsync(Guid? id = null)
    {
        ErrorBanner.IsVisible = false; _editing = id.HasValue ? await _svc.GetByIdAsync(id.Value) : null;
        var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty; _invoices.Clear(); _purchaseDocs.Clear();
        if (cid != Guid.Empty)
        {
            _invoices.AddRange((await _billing.GetAllAsync(cid)).Where(x => x.Status != InvoiceStatus.Cancelled && (x.DueAmount > 0 || x.LocalId == _editing?.InvoiceId)));
            _purchaseDocs.AddRange((await _purchases.GetAllAsync(cid)).Where(x => x.Status != PurchaseStatus.Cancelled && (x.DueAmount > 0 || x.LocalId == _editing?.InvoiceId)));
        }
        TypePicker.SelectedIndex = _editing?.IsSupplier == true ? 1 : 0; MethodPicker.SelectedIndex = _editing == null ? 0 : MethodIndex(_editing.Method);
        DatePayment.Date = _editing?.PaymentDate ?? DateTime.Today; EntryRef.Text = _editing?.Reference ?? string.Empty; EditorNotes.Text = _editing?.Notes ?? string.Empty;
        PopulateDocuments();
        if (_editing != null && _editing.InvoiceId.HasValue)
        {
            var index = _editing.IsSupplier ? _purchaseDocs.FindIndex(x => x.LocalId == _editing.InvoiceId.Value) : _invoices.FindIndex(x => x.LocalId == _editing.InvoiceId.Value);
            DocumentPicker.SelectedIndex = index;
            EntryAmount.Text = _editing.Amount.ToString("0.00");
            UpdateSelectedDocument(addEditingAmount: true);
        }
        else
        {
            DocumentPicker.SelectedIndex = -1; EntryParty.Text = string.Empty; EntryAmount.Text = string.Empty; LblOutstanding.Text = "₹0.00";
        }
    }

    private void OnTypeChanged(object? sender, EventArgs e)
    {
        if (_editing != null && ((TypePicker.SelectedIndex == 1) != _editing.IsSupplier)) _editing = null;
        PopulateDocuments(); DocumentPicker.SelectedIndex = -1; EntryParty.Text = string.Empty; EntryAmount.Text = string.Empty; LblOutstanding.Text = "₹0.00";
    }

    private void PopulateDocuments()
    {
        DocumentPicker.ItemsSource = TypePicker.SelectedIndex == 1
            ? _purchaseDocs.Select(x => $"{x.PurchaseNumber} • {x.SupplierName} • Due ₹{x.DueAmount:N2}").ToList()
            : _invoices.Select(x => $"{x.InvoiceNumber} • {x.CustomerName} • Due ₹{x.DueAmount:N2}").ToList();
    }

    private void OnDocumentChanged(object? sender, EventArgs e) => UpdateSelectedDocument(false);

    private void UpdateSelectedDocument(bool addEditingAmount)
    {
        var index = DocumentPicker.SelectedIndex;
        if (TypePicker.SelectedIndex == 1)
        {
            if (index < 0 || index >= _purchaseDocs.Count) return; var p = _purchaseDocs[index]; var due = p.DueAmount + (addEditingAmount && _editing?.InvoiceId == p.LocalId ? _editing.Amount : 0);
            EntryParty.Text = p.SupplierName; LblOutstanding.Text = $"₹{due:N2}"; if (!addEditingAmount) EntryAmount.Text = due.ToString("0.00");
        }
        else
        {
            if (index < 0 || index >= _invoices.Count) return; var inv = _invoices[index]; var due = inv.DueAmount + (addEditingAmount && _editing?.InvoiceId == inv.LocalId ? _editing.Amount : 0);
            EntryParty.Text = inv.CustomerName; LblOutstanding.Text = $"₹{due:N2}"; if (!addEditingAmount) EntryAmount.Text = due.ToString("0.00");
        }
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        ErrorBanner.IsVisible = false;
        if (TypePicker.SelectedIndex < 0 || DocumentPicker.SelectedIndex < 0) { ShowError("Select an invoice or purchase."); return; }
        if (!decimal.TryParse(EntryAmount.Text, out var amt) || amt <= 0) { ShowError("Enter a valid amount."); return; }
        if (MethodPicker.SelectedIndex < 0) { ShowError("Select a payment method."); return; }
        var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty; if (cid == Guid.Empty) { ShowError("Company is required."); return; }
        var isSupplier = TypePicker.SelectedIndex == 1; Guid docId; Guid partyId; string partyName; string docNumber; decimal outstanding;
        if (isSupplier)
        {
            if (DocumentPicker.SelectedIndex >= _purchaseDocs.Count) { ShowError("Select a valid purchase."); return; }
            var p = _purchaseDocs[DocumentPicker.SelectedIndex]; docId = p.LocalId; partyId = p.SupplierId; partyName = p.SupplierName; docNumber = p.PurchaseNumber; outstanding = p.DueAmount + (_editing?.InvoiceId == p.LocalId ? _editing.Amount : 0);
        }
        else
        {
            if (DocumentPicker.SelectedIndex >= _invoices.Count) { ShowError("Select a valid invoice."); return; }
            var inv = _invoices[DocumentPicker.SelectedIndex]; docId = inv.LocalId; partyId = inv.CustomerId; partyName = inv.CustomerName; docNumber = inv.InvoiceNumber; outstanding = inv.DueAmount + (_editing?.InvoiceId == inv.LocalId ? _editing.Amount : 0);
        }
        if (amt > outstanding) { ShowError($"Payment cannot exceed outstanding amount ₹{outstanding:N2}."); return; }
        var m = _editing ?? new PaymentModel { CompanyId = cid }; m.CompanyId = cid; m.IsSupplier = isSupplier; m.InvoiceId = docId; m.InvoiceNumber = docNumber; m.PartyId = partyId; m.PartyName = partyName;
        m.Amount = amt; m.PaymentDate = DatePayment.Date ?? DateTime.Today; m.Reference = EntryRef.Text?.Trim() ?? ""; m.Notes = EditorNotes.Text?.Trim() ?? ""; m.Status = PaymentStatus.Completed; m.Method = MethodFromIndex(MethodPicker.SelectedIndex);
        if (string.IsNullOrEmpty(m.PaymentNumber)) m.PaymentNumber = await _svc.GetNextPaymentNumberAsync(cid);
        var (ok, err) = _editing == null ? await _svc.CreateAsync(m) : await _svc.UpdateAsync(m); if (ok) await CloseAsync(); else ShowError(err ?? "Save failed.");
    }

    private static int MethodIndex(PaymentMethod method) => method switch { PaymentMethod.BankTransfer => 1, PaymentMethod.CreditCard => 2, PaymentMethod.DebitCard => 3, PaymentMethod.Cheque => 4, PaymentMethod.Online => 5, _ => 0 };
    private static PaymentMethod MethodFromIndex(int i) => i switch { 1 => PaymentMethod.BankTransfer, 2 => PaymentMethod.CreditCard, 3 => PaymentMethod.DebitCard, 4 => PaymentMethod.Cheque, 5 => PaymentMethod.Online, _ => PaymentMethod.Cash };
    private async Task CloseAsync() { if (CloseRequested != null) { CloseRequested.Invoke(); return; } await Shell.Current.GoToAsync(".."); }
    private async void OnBackClicked(object s, EventArgs e) => await CloseAsync();
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
