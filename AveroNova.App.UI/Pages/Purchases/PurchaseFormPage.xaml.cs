using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Purchases;

[QueryProperty(nameof(EditId), "id")]
public partial class PurchaseFormPage : ContentPage
{
    private readonly IPurchaseService _svc;
    private readonly ICompanyService  _company;
    private PurchaseModel? _editing;
    public string? EditId { get; set; }

    public PurchaseFormPage(IPurchaseService svc, ICompanyService company)
    { InitializeComponent(); _svc = svc; _company = company; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        DatePurchase.Date = DateTime.Today;
        DateDue.Date      = DateTime.Today.AddDays(30);
        if (!string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var id))
        {
            _editing = await _svc.GetByIdAsync(id);
            if (_editing != null)
            {
                LblTitle.Text       = "Edit Purchase";
                EntrySupplier.Text  = _editing.SupplierName;
                DatePurchase.Date   = _editing.PurchaseDate;
                DateDue.Date        = _editing.DueDate;
                EntryRef.Text       = _editing.Reference;
                EditorNotes.Text    = _editing.Notes;
            }
        }
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntrySupplier.Text)) { ShowError("Supplier name is required."); return; }
        var cid   = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        var model = _editing ?? new PurchaseModel { CompanyId = cid };
        model.SupplierName  = EntrySupplier.Text.Trim();
        //model.PurchaseDate  = DatePurchase.Date;
        //model.DueDate       = DateDue.Date;
        model.Reference     = EntryRef.Text?.Trim() ?? "";
        model.Notes         = EditorNotes.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(model.PurchaseNumber)) model.PurchaseNumber = await _svc.GetNextPurchaseNumberAsync(cid);

        var (ok, err) = _editing == null ? await _svc.CreateAsync(model) : await _svc.UpdateAsync(model);
        if (ok) await Shell.Current.GoToAsync("..");
        else ShowError(err ?? "Save failed.");
    }

    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
