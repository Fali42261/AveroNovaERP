using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Payments;

// OFFLINE: Payments are persisted and reconciled locally, then synchronized when connectivity is restored.

[QueryProperty(nameof(EditId), "id")]
[QueryProperty(nameof(InitialInvoiceId), "invoiceId")]
[QueryProperty(nameof(InitialPurchaseId), "purchaseId")]
public partial class PaymentFormPage : ContentPage
{
    private readonly IPaymentService _svc;
    private readonly ICompanyService _company;
    private readonly IBillingService _billing;
    private readonly IPurchaseService _purchasesService;
    private List<InvoiceModel> _invoices = [];
    private List<PurchaseModel> _purchases = [];
    private bool _supplierMode;
    private PaymentModel? _editing;
    public string? EditId { get; set; }
    public string? InitialInvoiceId { get; set; }
    public string? InitialPurchaseId { get; set; }

    public PaymentFormPage(IPaymentService svc, ICompanyService company, IBillingService billing, IPurchaseService purchasesService)
    { InitializeComponent(); _svc = svc; _company = company; _billing = billing; _purchasesService = purchasesService; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        DatePayment.Date = DateTime.Today;
        var companyId = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        if (!string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var id))
            _editing = await _svc.GetByIdAsync(id);
        _supplierMode = _editing?.IsSupplier == true || Guid.TryParse(InitialPurchaseId, out _);
        if (_supplierMode)
        {
            DocumentLabel.Text = "Purchase *";
            var editingPurchaseId = _editing?.InvoiceId;
            _purchases = (await _purchasesService.GetAllAsync(companyId))
                .Where(p => (p.Status is not PurchaseStatus.Draft and not PurchaseStatus.Cancelled && p.DueAmount > 0)
                            || p.LocalId == editingPurchaseId)
                .OrderByDescending(p => p.PurchaseDate).ToList();
            InvoicePicker.ItemsSource = _purchases;
            InvoicePicker.ItemDisplayBinding = new Binding(nameof(PurchaseModel.PurchaseNumber));
            var selectedId = _editing?.InvoiceId ?? (Guid.TryParse(InitialPurchaseId, out var purchaseId) ? purchaseId : null);
            InvoicePicker.SelectedItem = _purchases.FirstOrDefault(p => p.LocalId == selectedId);
        }
        else
        {
            _invoices = (await _billing.GetAllAsync(companyId))
                .Where(i => i.Status is not InvoiceStatus.Draft and not InvoiceStatus.Cancelled
                            && (i.Status != InvoiceStatus.Paid || i.LocalId == _editing?.InvoiceId))
                .OrderByDescending(i => i.InvoiceDate).ToList();
            InvoicePicker.ItemsSource = _invoices;
            InvoicePicker.ItemDisplayBinding = new Binding(nameof(InvoiceModel.InvoiceNumber));
        }
        if (_editing is not null)
        {
            EntryParty.Text      = _editing.PartyName;
            EntryAmount.Text     = _editing.Amount.ToString("N2");
            DatePayment.Date     = _editing.PaymentDate;
            EntryRef.Text        = _editing.Reference;
            EditorNotes.Text     = _editing.Notes;
            MethodPicker.SelectedIndex = MethodIndex(_editing.Method);
            if (!_supplierMode)
                InvoicePicker.SelectedItem = _editing.InvoiceId is Guid invoiceId
                    ? _invoices.FirstOrDefault(i => i.LocalId == invoiceId) : null;
        }
        else if (!_supplierMode && Guid.TryParse(InitialInvoiceId, out var initialInvoiceId))
            InvoicePicker.SelectedItem = _invoices.FirstOrDefault(i => i.LocalId == initialInvoiceId);
    }

    private void OnInvoiceChanged(object? sender, EventArgs e)
    {
        if (InvoicePicker.SelectedItem is PurchaseModel purchase)
        {
            EntryParty.Text = purchase.SupplierName;
            if (_editing is null) EntryAmount.Text = Math.Max(0, purchase.DueAmount).ToString("N2");
            return;
        }
        if (InvoicePicker.SelectedItem is not InvoiceModel invoice)
            return;
        EntryParty.Text = invoice.CustomerName;
        if (_editing is null)
            EntryAmount.Text = Math.Max(0, invoice.DueAmount).ToString("N2");
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryParty.Text))  { ShowError("Party name is required."); return; }
        if (!decimal.TryParse(EntryAmount.Text, out var amt) || amt <= 0) { ShowError("Enter a valid amount."); return; }
        if (MethodPicker.SelectedIndex < 0) { ShowError("Select a payment method."); return; }

        var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        var m   = _editing ?? new PaymentModel { CompanyId = cid };
        m.PartyName      = EntryParty.Text.Trim();
        var invoice = InvoicePicker.SelectedItem as InvoiceModel;
        var purchase = InvoicePicker.SelectedItem as PurchaseModel;
        m.InvoiceId      = purchase?.LocalId ?? invoice?.LocalId;
        m.InvoiceNumber  = purchase?.PurchaseNumber ?? invoice?.InvoiceNumber ?? "";
        m.PartyId        = purchase?.SupplierId ?? invoice?.CustomerId ?? m.PartyId;
        m.IsSupplier     = purchase is not null;
        m.Amount         = amt;
        m.PaymentDate     = DatePayment.Date ?? DateTime.Today;
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

    private static int MethodIndex(PaymentMethod method)
        => method switch
        {
            PaymentMethod.Cash => 0,
            PaymentMethod.BankTransfer => 1,
            PaymentMethod.CreditCard => 2,
            PaymentMethod.DebitCard => 3,
            PaymentMethod.Cheque => 4,
            PaymentMethod.Online => 5,
            _ => -1
        };
}
