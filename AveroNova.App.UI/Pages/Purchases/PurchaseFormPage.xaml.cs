using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Purchases;

[QueryProperty(nameof(EditId), "id")]
public partial class PurchaseFormPage : ContentPage
{
    private readonly IPurchaseService _svc;
    private readonly ICompanyService _company;
    private PurchaseModel? _editing;
    public string? EditId { get; set; }
    public Action? CloseRequested { get; set; }

    public PurchaseFormPage(IPurchaseService svc, ICompanyService company)
    { InitializeComponent(); _svc = svc; _company = company; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Guid? id = !string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var parsed) ? parsed : null;
        await LoadAsync(id);
    }

    public async Task LoadAsync(Guid? id = null)
    {
        ErrorBanner.IsVisible = false;
        DatePurchase.Date = DateTime.Today;
        DateDue.Date = DateTime.Today.AddDays(30);
        EntrySupplier.Text = string.Empty; EntryRef.Text = string.Empty; EditorNotes.Text = string.Empty; _editing = null;
        if (id.HasValue && id.Value != Guid.Empty)
        {
            _editing = await _svc.GetByIdAsync(id.Value);
            if (_editing != null)
            {
                LblTitle.Text = "Edit Purchase"; EntrySupplier.Text = _editing.SupplierName;
                DatePurchase.Date = _editing.PurchaseDate; DateDue.Date = _editing.DueDate;
                EntryRef.Text = _editing.Reference; EditorNotes.Text = _editing.Notes;
            }
        }
        else LblTitle.Text = "New Purchase";
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        ErrorBanner.IsVisible = false;
        if (string.IsNullOrWhiteSpace(EntrySupplier.Text)) { ShowError("Supplier name is required."); return; }
        var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        if (cid == Guid.Empty) { ShowError("Company is required."); return; }
        var model = _editing ?? new PurchaseModel { CompanyId = cid };
        model.CompanyId = cid; model.SupplierName = EntrySupplier.Text.Trim();
        model.PurchaseDate = DatePurchase.Date ?? DateTime.Today; model.DueDate = DateDue.Date ?? DateTime.Today.AddDays(30);
        model.Reference = EntryRef.Text?.Trim() ?? ""; model.Notes = EditorNotes.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(model.PurchaseNumber)) model.PurchaseNumber = await _svc.GetNextPurchaseNumberAsync(cid);
        var (ok, err) = _editing == null ? await _svc.CreateAsync(model) : await _svc.UpdateAsync(model);
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
