using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Company;

[QueryProperty(nameof(EditId), "id")]
public partial class CompanyFormPage : ContentPage
{
    private readonly ICompanyService _svc;
    private CompanyModel? _editing;

    public string? EditId { get; set; }

    public CompanyFormPage(ICompanyService svc) { InitializeComponent(); _svc = svc; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var id))
        {
            _editing = await _svc.GetByIdAsync(id);
            if (_editing != null)
            {
                LblTitle.Text      = "Edit Company";
                EntryName.Text     = _editing.Name;
                EntryEmail.Text    = _editing.Email;
                EntryPhone.Text    = _editing.Phone;
                EntryWebsite.Text  = _editing.Website;
                EntryAddress.Text  = _editing.Address;
                EntryCity.Text     = _editing.City;
                EntryCountry.Text  = _editing.Country;
                EntryTax.Text      = _editing.TaxNumber;
                EntryRegNo.Text    = _editing.RegistrationNo;
                EntryCurrency.Text = _editing.Currency;
                EntryInvPrefix.Text= _editing.InvoicePrefix;
            }
        }
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryName.Text)) { ShowError("Company name is required."); return; }

        var model = _editing ?? new CompanyModel();
        model.Name           = EntryName.Text.Trim();
        model.Email          = EntryEmail.Text?.Trim() ?? "";
        model.Phone          = EntryPhone.Text?.Trim() ?? "";
        model.Website        = EntryWebsite.Text?.Trim() ?? "";
        model.Address        = EntryAddress.Text?.Trim() ?? "";
        model.City           = EntryCity.Text?.Trim() ?? "";
        model.Country        = EntryCountry.Text?.Trim() ?? "";
        model.TaxNumber      = EntryTax.Text?.Trim() ?? "";
        model.RegistrationNo = EntryRegNo.Text?.Trim() ?? "";
        model.Currency       = EntryCurrency.Text?.Trim() is { Length: > 0 } c ? c : "USD";
        model.InvoicePrefix  = EntryInvPrefix.Text?.Trim() is { Length: > 0 } p ? p : "INV";

        var (ok, error) = _editing == null
            ? await _svc.CreateAsync(model)
            : await _svc.UpdateAsync(model);

        if (ok) await Shell.Current.GoToAsync("..");
        else ShowError(error ?? "Save failed.");
    }

    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
